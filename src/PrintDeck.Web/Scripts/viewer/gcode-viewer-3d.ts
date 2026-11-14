// 3D toolpath view with three.js (vendored in wwwroot/lib/three, resolved through the import map in
// App.razor). Each extrusion move is one instance of an 8-sided cylinder, scaled to the move's
// length, line width and layer height. Scrubbing only changes the instance count; print progress
// recolours just the instances that changed state.

import * as THREE from "three";
import { OrbitControls } from "three/addons/controls/OrbitControls.js";

import { type Rgba, cssVar, over, rgba } from "./colors.js";
import { type Bed, type FitTarget, type Renderer, type Toolpath, type ViewState, KINDS, clamp, lastPrinted, layerOf, lowerBound, nozzlePoint } from "./toolpath.js";

const LINE_MM = 0.45;
const GRID_MM = 10;

type Rgb = [number, number, number];

/** three.js r170 needs WebGL 2. */
export function supported(): boolean {
    try { return !!document.createElement("canvas").getContext("webgl2"); } catch { return false; }
}

/** Instances built from a toolpath: one per extrusion move, in file order. */
interface Beads {
    mesh: THREE.InstancedMesh;
    /** x0 y0 x1 y1 z h per instance. */
    seg: Float32Array;
    /** Toolpath point index of each instance (ascending). */
    index: Uint32Array;
    kind: Uint8Array;
    maxZ: number;
}

export class Viewer3D implements Renderer {
    active = false;
    private readonly renderer: THREE.WebGLRenderer;
    private readonly scene = new THREE.Scene();
    private readonly camera = new THREE.PerspectiveCamera(40, 1, 0.1, 5000);
    private readonly controls: OrbitControls;
    private readonly bedPlane: THREE.Mesh<THREE.PlaneGeometry, THREE.MeshBasicMaterial>;
    private readonly bedGrid: THREE.LineSegments<THREE.BufferGeometry, THREE.LineBasicMaterial>;
    private readonly bedEdge: THREE.LineLoop<THREE.BufferGeometry, THREE.LineBasicMaterial>;
    private readonly nozzle: THREE.Mesh<THREE.SphereGeometry, THREE.MeshBasicMaterial>;
    private readonly head = nozzleModel();
    private readonly resizeObserver: ResizeObserver;
    private readonly scheme = matchMedia("(prefers-color-scheme: dark)");
    private readonly onScheme = () => { this.colors = null; this.draw(); };
    private readonly onDblClick = () => this.fit("model");

    private data: Toolpath | null = null;
    private beads: Beads | null = null;
    private colors: { kind: Rgb[]; upcoming: Rgb } | null = null;
    /** What the instance buffers currently reflect, to update only what changed. */
    private written: { hidden: string; colors: object; printed: number } | null = null;
    private sized = false;
    private frame = 0;

    constructor(private readonly canvas: HTMLCanvasElement, private readonly bed: Bed, private readonly state: ViewState) {
        this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true });
        this.renderer.setPixelRatio(window.devicePixelRatio || 1);
        this.camera.up.set(0, 0, 1);
        this.camera.position.set(bed.w / 2 - 150, -200, 250);

        this.controls = new OrbitControls(this.camera, canvas);
        this.controls.screenSpacePanning = true;
        this.controls.target.set(bed.w / 2, bed.d / 2, 0);
        this.controls.addEventListener("change", () => this.draw());

        const sun = new THREE.DirectionalLight(0xffffff, 2.2);
        sun.position.set(0.35, -0.55, 0.75);
        const fill = new THREE.DirectionalLight(0xffffff, 0.6);
        fill.position.set(-0.6, 0.4, 0.3);
        this.scene.add(new THREE.HemisphereLight(0xffffff, 0x404040, 1.1), sun, fill);

        const { w, d } = bed;
        this.bedPlane = new THREE.Mesh(new THREE.PlaneGeometry(w, d), new THREE.MeshBasicMaterial());
        this.bedPlane.position.set(w / 2, d / 2, -0.02);
        const grid: number[] = [];
        for (let x = GRID_MM; x < w; x += GRID_MM) grid.push(x, 0, 0, x, d, 0);
        for (let y = GRID_MM; y < d; y += GRID_MM) grid.push(0, y, 0, w, y, 0);
        this.bedGrid = new THREE.LineSegments(lines(grid), new THREE.LineBasicMaterial({ transparent: true, depthWrite: false }));
        this.bedEdge = new THREE.LineLoop(lines([0, 0, 0, w, 0, 0, w, d, 0, 0, d, 0]), new THREE.LineBasicMaterial({ transparent: true, depthWrite: false }));

        this.nozzle = new THREE.Mesh(new THREE.SphereGeometry(0.9, 16, 8), new THREE.MeshBasicMaterial({ depthTest: false }));
        this.nozzle.renderOrder = 10;
        this.nozzle.visible = false;
        this.head.visible = false;
        this.scene.add(this.bedPlane, this.bedGrid, this.bedEdge, this.nozzle, this.head);

        canvas.addEventListener("dblclick", this.onDblClick);
        this.resizeObserver = new ResizeObserver(() => this.resize());
        this.resizeObserver.observe(canvas);
        this.scheme.addEventListener("change", this.onScheme);
    }

    setData(data: Toolpath): void {
        this.data = data;
        this.disposeBeads();

        let n = 0;
        for (let i = 1; i < data.kinds.length; i++) if (data.kinds[i] !== 0) n++;
        const seg = new Float32Array(n * 6);
        const index = new Uint32Array(n), kind = new Uint8Array(n);
        let k = 0, maxZ = 0;
        for (let L = 0; L < data.layerCount; L++) {
            const z = data.z[L];
            const h = clamp(L === 0 ? z : z - data.z[L - 1], 0.05, 1.2); // bead height = gap to the layer below
            maxZ = Math.max(maxZ, z);
            const end = data.start[L] + data.count[L];
            for (let i = Math.max(data.start[L] + 1, 1); i < end; i++) {
                if (data.kinds[i] === 0) continue;
                seg.set([data.xy[(i - 1) * 2], data.xy[(i - 1) * 2 + 1], data.xy[i * 2], data.xy[i * 2 + 1], z, h], k * 6);
                index[k] = i;
                kind[k++] = data.kinds[i];
            }
        }

        // Unit bead along +X; cross-section is Y (width) × Z (height).
        const bead = new THREE.CylinderGeometry(0.5, 0.5, 1, 8, 1, false).rotateZ(Math.PI / 2);
        const mesh = new THREE.InstancedMesh(bead, new THREE.MeshLambertMaterial(), Math.max(n, 1));
        mesh.instanceColor = new THREE.InstancedBufferAttribute(new Float32Array(Math.max(n, 1) * 3), 3);
        mesh.frustumCulled = false;
        mesh.count = 0;
        this.scene.add(mesh);

        this.beads = { mesh, seg, index, kind, maxZ };
        this.written = null;
        this.fit("model");
    }

    fit(target: FitTarget): void {
        const d = this.data;
        const b = target === "model" && d ? (d.modelBounds ?? d.bounds) : { minX: 0, minY: 0, maxX: this.bed.w, maxY: this.bed.d };
        const top = this.beads?.maxZ ?? 0;
        const center = new THREE.Vector3((b.minX + b.maxX) / 2, (b.minY + b.maxY) / 2, top / 2);
        const radius = Math.hypot(b.maxX - b.minX, b.maxY - b.minY, top) / 2 || 50;
        const fov = THREE.MathUtils.degToRad(this.camera.fov);
        const vfov = Math.min(fov, 2 * Math.atan(Math.tan(fov / 2) * this.camera.aspect));
        const dist = radius / Math.sin(vfov / 2) * 1.05;

        const dir = this.camera.position.clone().sub(this.controls.target).normalize();
        this.controls.target.copy(center);
        this.camera.position.copy(center).addScaledVector(dir, dist);
        this.camera.near = Math.max(dist / 200, 0.05);
        this.camera.far = dist * 20 + 2000;
        this.camera.updateProjectionMatrix();
        this.controls.update();
        this.draw();
    }

    draw(): void {
        if (this.frame || !this.active) return;
        this.frame = requestAnimationFrame(() => { this.frame = 0; this.render(); });
    }

    dispose(): void {
        cancelAnimationFrame(this.frame);
        this.resizeObserver.disconnect();
        this.scheme.removeEventListener("change", this.onScheme);
        this.canvas.removeEventListener("dblclick", this.onDblClick);
        this.controls.dispose();
        this.disposeBeads();
        this.scene.traverse(o => {
            if (o instanceof THREE.Mesh || o instanceof THREE.Line) { o.geometry.dispose(); (o.material as THREE.Material).dispose(); }
        });
        this.renderer.dispose();
        this.renderer.forceContextLoss();
        this.data = null;
    }

    // ───────────── internals ─────────────

    private resize(): void {
        const rect = this.canvas.getBoundingClientRect();
        if (rect.width === 0 || rect.height === 0) return; // hidden (2D mode)
        this.renderer.setPixelRatio(window.devicePixelRatio || 1);
        this.renderer.setSize(rect.width, rect.height, false);
        this.camera.aspect = rect.width / rect.height;
        this.camera.updateProjectionMatrix();
        if (!this.sized) { this.sized = true; this.fit(this.data ? "model" : "bed"); }
        this.draw();
    }

    private readColors(): { kind: Rgb[]; upcoming: Rgb } {
        const v = (name: string) => rgba(cssVar(this.canvas, name));
        const bed = v("--tp-bed");
        setColor(this.bedPlane.material, bed);
        setColor(this.bedGrid.material, v("--tp-grid"));
        setColor(this.bedEdge.material, v("--tp-edge"));
        setColor(this.nozzle.material, v("--tp-nozzle"));
        return {
            kind: Array.from({ length: KINDS }, (_, k) => linear(v(`--tp-${k}`))),
            upcoming: linear(over(v("--tp-upcoming"), bed)),
        };
    }

    private render(): void {
        if (!this.sized) return;
        const colors = (this.colors ??= this.readColors());
        const { data: d, beads, state: s } = this;

        if (d && beads) {
            const hidden = [...s.opts.hidden].sort().join(",");
            const printed = s.progress == null ? Infinity : lastPrinted(d, s.progress);
            const w = this.written;
            if (w?.hidden !== hidden) this.writeMatrices(beads, new Set(s.opts.hidden));
            if (w?.colors !== colors) this.writeColors(beads, 0, beads.index.length, printed, colors);
            else if (w.printed !== printed) {
                // only instances between the old and new progress change colour
                const lo = Math.min(printed, w.printed), hi = Math.max(printed, w.printed);
                const from = lowerBound(beads.index, Number.isFinite(lo) ? lo : 0);
                const to = Number.isFinite(hi) ? lowerBound(beads.index, hi + 1) : beads.index.length;
                if (to > from) this.writeColors(beads, from, to, printed, colors);
            }
            this.written = { hidden, colors, printed };

            const layer = clamp(s.layer, 0, Math.max(d.layerCount - 1, 0));
            const cut = d.layerCount ? d.start[layer] + Math.round(d.count[layer] * s.fraction) : 0;
            beads.mesh.count = lowerBound(beads.index, cut);

            const tip = nozzlePoint(d, s);
            const at = tip >= 0 ? [d.xy[tip * 2], d.xy[tip * 2 + 1], d.z[layerOf(d, tip)]] as const : null;
            this.head.visible = !!at && s.opts.head;
            this.nozzle.visible = !!at && !s.opts.head;
            if (at) {
                this.head.position.set(at[0], at[1], at[2]);
                this.nozzle.position.set(at[0], at[1], at[2] + 0.3);
            }
        }

        this.renderer.render(this.scene, this.camera);
    }

    /** Instance transforms; hidden kinds collapse to zero scale. */
    private writeMatrices(b: Beads, hidden: Set<number>): void {
        const m = b.mesh.instanceMatrix.array as Float32Array, s = b.seg;
        for (let k = 0; k < b.kind.length; k++) {
            const o = k * 16, p = k * 6;
            if (hidden.has(b.kind[k])) { m.fill(0, o, o + 16); continue; }
            const dx = s[p + 2] - s[p], dy = s[p + 3] - s[p + 1];
            const len = Math.hypot(dx, dy);
            const c = len > 1e-6 ? dx / len : 1, sn = len > 1e-6 ? dy / len : 0;
            const sx = len + LINE_MM * 0.5, sy = LINE_MM, sz = s[p + 5];
            // column-major: rotate about Z, scale, translate to the bead centre
            m.set([
                c * sx, sn * sx, 0, 0,
                -sn * sy, c * sy, 0, 0,
                0, 0, sz, 0,
                (s[p] + s[p + 2]) / 2, (s[p + 1] + s[p + 3]) / 2, s[p + 4] - sz / 2, 1,
            ], o);
        }
        b.mesh.instanceMatrix.needsUpdate = true;
    }

    /** Colours instances [from, to): kind colour once printed (or with no job), else "upcoming". */
    private writeColors(b: Beads, from: number, to: number, printed: number, colors: { kind: Rgb[]; upcoming: Rgb }): void {
        const attr = b.mesh.instanceColor!;
        const a = attr.array as Float32Array;
        for (let k = from; k < to; k++) a.set(b.index[k] > printed ? colors.upcoming : colors.kind[b.kind[k]], k * 3);
        attr.addUpdateRange(from * 3, (to - from) * 3);
        attr.needsUpdate = true;
    }

    private disposeBeads(): void {
        if (!this.beads) return;
        const { mesh } = this.beads;
        this.scene.remove(mesh);
        mesh.geometry.dispose();
        (mesh.material as THREE.Material).dispose();
        mesh.dispose();
        this.beads = null;
    }
}

/** A brass nozzle (cone + hex), origin at the tip, to scale in mm. */
function nozzleModel(): THREE.Group {
    const brass = new THREE.MeshLambertMaterial({ color: 0xc9a227 });
    // Cylinders in three.js stand along Y; rotate the geometry to stand along Z.
    const upright = (g: THREE.BufferGeometry) => g.rotateX(Math.PI / 2);
    const part = (geometry: THREE.BufferGeometry, z: number) => {
        const m = new THREE.Mesh(geometry, brass);
        m.position.z = z;
        return m;
    };
    const head = new THREE.Group();
    head.add(
        part(upright(new THREE.CylinderGeometry(1.6, 0.25, 2.2, 16)), 1.1), // cone
        part(upright(new THREE.CylinderGeometry(3.5, 3.5, 3, 6)), 3.7),    // hex
    );
    return head;
}

function lines(points: number[]): THREE.BufferGeometry {
    return new THREE.BufferGeometry().setAttribute("position", new THREE.Float32BufferAttribute(points, 3));
}

/** sRGB → three.js working (linear) colour space, for instance colours. */
function linear([r, g, b]: Rgba): Rgb {
    const c = new THREE.Color().setRGB(r, g, b, THREE.SRGBColorSpace);
    return [c.r, c.g, c.b];
}

function setColor(material: THREE.MeshBasicMaterial | THREE.LineBasicMaterial, [r, g, b, a]: Rgba): void {
    material.color.setRGB(r, g, b, THREE.SRGBColorSpace);
    material.opacity = a;
    material.transparent = a < 1;
}
