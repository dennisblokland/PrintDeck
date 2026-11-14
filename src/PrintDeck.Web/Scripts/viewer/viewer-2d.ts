// Top-down view of one layer on a 2D canvas, with the layer below dimmed and optional travel moves.

import { cssVar } from "./colors.js";
import { type Bed, type FitTarget, type Renderer, type Toolpath, type ViewState, KINDS, clamp, nozzlePoint } from "./toolpath.js";

const LINE_MM = 0.45;
const GRID_MM = 10;

interface Palette {
    kind: string[];
    bed: string;
    grid: string;
    edge: string;
    ghost: string;
    upcoming: string;
    nozzle: string;
    head: string;
}

interface Point {
    x: number;
    y: number;
}

export class Viewer2D implements Renderer {
    active = false;
    private readonly ctx: CanvasRenderingContext2D;
    private data: Toolpath | null = null;
    /** Screen = world × scale + (x, y), with Y flipped (bed origin at the bottom left). */
    private readonly view = { scale: 1, x: 0, y: 0 };
    private readonly pointers = new Map<number, Point>();
    private readonly unlisten: (() => void)[] = [];
    private readonly resizeObserver: ResizeObserver;
    private readonly scheme = matchMedia("(prefers-color-scheme: dark)");
    private readonly onScheme = () => { this.colors = null; this.draw(); };
    private colors: Palette | null = null;
    private size: { w: number; h: number } | null = null;
    private fitted = false;
    private frame = 0;

    constructor(private readonly canvas: HTMLCanvasElement, private readonly bed: Bed, private readonly state: ViewState) {
        this.ctx = canvas.getContext("2d")!;
        this.resizeObserver = new ResizeObserver(() => this.resize());
        this.resizeObserver.observe(canvas);
        this.scheme.addEventListener("change", this.onScheme);

        this.listen("wheel", e => this.onWheel(e), { passive: false });
        this.listen("pointerdown", e => this.onPointerDown(e));
        this.listen("pointermove", e => this.onPointerMove(e));
        this.listen("pointerup", e => this.onPointerUp(e));
        this.listen("pointercancel", e => this.onPointerUp(e));
        this.listen("dblclick", () => this.fit("model"));
        this.resize();
    }

    setData(data: Toolpath): void {
        this.data = data;
        this.fit("bed");
        this.draw();
    }

    fit(target: FitTarget): void {
        const rect = this.canvas.getBoundingClientRect();
        if (rect.width === 0 || rect.height === 0) return;
        let { minX, minY, maxX, maxY } = { minX: 0, minY: 0, maxX: this.bed.w, maxY: this.bed.d };
        const b = this.data?.bounds;
        if (b && b.maxX > b.minX) {
            if (target === "model") ({ minX, minY, maxX, maxY } = this.data!.modelBounds ?? b);
            else { minX = Math.min(minX, b.minX); minY = Math.min(minY, b.minY); maxX = Math.max(maxX, b.maxX); maxY = Math.max(maxY, b.maxY); }
        }
        const pad = target === "model" ? 32 : 16;
        const w = Math.max(maxX - minX, 1), h = Math.max(maxY - minY, 1);
        const scale = Math.min((rect.width - pad * 2) / w, (rect.height - pad * 2) / h);
        this.view.scale = scale;
        this.view.x = rect.width / 2 - (minX + w / 2) * scale;
        this.view.y = rect.height / 2 + (minY + h / 2) * scale;
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
        this.unlisten.forEach(f => f());
        this.data = null;
    }

    // ───────────── interaction ─────────────

    private listen<K extends keyof HTMLElementEventMap>(type: K, fn: (e: HTMLElementEventMap[K]) => void, options?: AddEventListenerOptions): void {
        this.canvas.addEventListener(type, fn, options);
        this.unlisten.push(() => this.canvas.removeEventListener(type, fn, options));
    }

    private resize(): void {
        const rect = this.canvas.getBoundingClientRect();
        if (rect.width === 0 || rect.height === 0) return; // hidden (3D mode)
        const dpr = window.devicePixelRatio || 1;
        const w = Math.round(rect.width * dpr), h = Math.round(rect.height * dpr);
        if (this.canvas.width !== w || this.canvas.height !== h) {
            this.canvas.width = w;
            this.canvas.height = h;
            if (this.size) {
                // keep the centre of the view where it was
                this.view.x += (rect.width - this.size.w) / 2;
                this.view.y += (rect.height - this.size.h) / 2;
            }
            this.size = { w: rect.width, h: rect.height };
            if (!this.fitted) { this.fitted = true; this.fit("bed"); }
        }
        this.draw();
    }

    private zoomAt(sx: number, sy: number, factor: number): void {
        const v = this.view;
        const scale = clamp(v.scale * factor, 0.2, 400);
        factor = scale / v.scale;
        v.x = sx - (sx - v.x) * factor;
        v.y = sy - (sy - v.y) * factor;
        v.scale = scale;
        this.draw();
    }

    private onWheel(e: WheelEvent): void {
        e.preventDefault();
        const r = this.canvas.getBoundingClientRect();
        this.zoomAt(e.clientX - r.left, e.clientY - r.top, Math.exp(-e.deltaY * (e.deltaMode === 1 ? 0.05 : 0.0015)));
    }

    private onPointerDown(e: PointerEvent): void {
        this.canvas.setPointerCapture(e.pointerId);
        this.pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
        this.canvas.classList.add("is-panning");
    }

    private onPointerMove(e: PointerEvent): void {
        const prev = this.pointers.get(e.pointerId);
        if (!prev) return;
        const next = { x: e.clientX, y: e.clientY };
        if (this.pointers.size === 1) {
            this.view.x += next.x - prev.x;
            this.view.y += next.y - prev.y;
            this.pointers.set(e.pointerId, next);
            this.draw();
        } else if (this.pointers.size === 2) {
            const other = [...this.pointers].find(([id]) => id !== e.pointerId)![1];
            const before = Math.hypot(prev.x - other.x, prev.y - other.y);
            const after = Math.hypot(next.x - other.x, next.y - other.y);
            this.pointers.set(e.pointerId, next);
            const r = this.canvas.getBoundingClientRect();
            if (before > 0) this.zoomAt((next.x + other.x) / 2 - r.left, (next.y + other.y) / 2 - r.top, after / before);
        }
    }

    private onPointerUp(e: PointerEvent): void {
        this.pointers.delete(e.pointerId);
        if (this.pointers.size === 0) this.canvas.classList.remove("is-panning");
    }

    // ───────────── drawing ─────────────

    private readColors(): Palette {
        const v = (name: string) => cssVar(this.canvas, name);
        return {
            kind: Array.from({ length: KINDS }, (_, k) => v(`--tp-${k}`)),
            bed: v("--tp-bed"), grid: v("--tp-grid"), edge: v("--tp-edge"),
            ghost: v("--tp-ghost"), upcoming: v("--tp-upcoming"), nozzle: v("--tp-nozzle"), head: v("--tp-head"),
        };
    }

    private render(): void {
        const { canvas, ctx, view, bed } = this;
        const { layer, fraction, progress: bytes, opts } = this.state;
        const dpr = window.devicePixelRatio || 1;
        const c = (this.colors ??= this.readColors());
        ctx.setTransform(1, 0, 0, 1, 0, 0);
        ctx.clearRect(0, 0, canvas.width, canvas.height);

        const s = view.scale * dpr;
        ctx.setTransform(s, 0, 0, -s, view.x * dpr, view.y * dpr);
        const px = 1 / s; // one device pixel in mm

        // bed
        ctx.fillStyle = c.bed;
        ctx.fillRect(0, 0, bed.w, bed.d);
        ctx.beginPath();
        for (let x = GRID_MM; x < bed.w; x += GRID_MM) { ctx.moveTo(x, 0); ctx.lineTo(x, bed.d); }
        for (let y = GRID_MM; y < bed.d; y += GRID_MM) { ctx.moveTo(0, y); ctx.lineTo(bed.w, y); }
        ctx.strokeStyle = c.grid;
        ctx.lineWidth = px;
        ctx.stroke();
        ctx.strokeStyle = c.edge;
        ctx.lineWidth = px * dpr;
        ctx.strokeRect(0, 0, bed.w, bed.d);

        const d = this.data;
        if (!d || d.layerCount === 0) return;

        ctx.lineCap = "round";
        ctx.lineJoin = "round";
        const width = Math.max(LINE_MM, 1.25 * px * dpr);
        const hidden = new Set(opts.hidden);

        if (opts.previous && layer > 0) {
            const prev = layer - 1;
            const path = new Path2D();
            this.trace(path, d.start[prev], d.start[prev] + d.count[prev], k => k !== 0 && !hidden.has(k));
            ctx.strokeStyle = c.ghost;
            ctx.lineWidth = width;
            ctx.stroke(path);
        }

        const start = d.start[layer];
        const end = start + Math.round(d.count[layer] * fraction);

        // printed / visible segments by kind, not-yet-printed in one muted path
        const paths: (Path2D | null)[] = Array.from({ length: KINDS }, () => null);
        const upcoming = new Path2D();
        let anyUpcoming = false;
        for (let i = Math.max(start + 1, 1); i < end; i++) {
            const k = d.kinds[i];
            if (k === 0 || hidden.has(k)) continue;
            const x0 = d.xy[(i - 1) * 2], y0 = d.xy[(i - 1) * 2 + 1], x1 = d.xy[i * 2], y1 = d.xy[i * 2 + 1];
            if (bytes != null && d.offsets[i] > bytes) {
                upcoming.moveTo(x0, y0); upcoming.lineTo(x1, y1); anyUpcoming = true;
            } else {
                const p = (paths[k] ??= new Path2D());
                p.moveTo(x0, y0); p.lineTo(x1, y1);
            }
        }

        ctx.lineWidth = width;
        if (anyUpcoming) { ctx.strokeStyle = c.upcoming; ctx.stroke(upcoming); }
        paths.forEach((p, k) => {
            if (!p) return;
            ctx.strokeStyle = c.kind[k];
            ctx.stroke(p);
        });

        if (opts.travel) {
            const travel = new Path2D();
            this.trace(travel, start, end, k => k === 0);
            ctx.setLineDash([px * dpr * 4, px * dpr * 4]);
            ctx.strokeStyle = c.kind[0];
            ctx.lineWidth = px * dpr;
            ctx.stroke(travel);
            ctx.setLineDash([]);
        }

        // nozzle: where the job is (when on this layer), or the end of the scrubbed range
        let tip = nozzlePoint(d, this.state);
        if (tip < start || tip >= start + d.count[layer]) tip = -1;
        if (tip >= 0 && opts.head) {
            // top-down outline of the nozzle hex (see gcode-viewer-3d.ts)
            const x = d.xy[tip * 2], y = d.xy[tip * 2 + 1];
            const hex = new Path2D();
            for (let i = 0; i < 6; i++) {
                const a = Math.PI / 6 + i * Math.PI / 3;
                hex.lineTo(x + 3.5 * Math.cos(a), y + 3.5 * Math.sin(a));
            }
            hex.closePath();
            ctx.fillStyle = c.head;
            ctx.fill(hex);
            ctx.strokeStyle = c.nozzle;
            ctx.lineWidth = px * dpr;
            ctx.globalAlpha = 0.45;
            ctx.stroke(hex);
            ctx.globalAlpha = 1;
        }
        if (tip >= 0) {
            ctx.setTransform(1, 0, 0, 1, 0, 0);
            const sx = (d.xy[tip * 2] * view.scale + view.x) * dpr;
            const sy = (-d.xy[tip * 2 + 1] * view.scale + view.y) * dpr;
            ctx.beginPath();
            ctx.arc(sx, sy, 5 * dpr, 0, Math.PI * 2);
            ctx.fillStyle = c.nozzle;
            ctx.fill();
            ctx.lineWidth = 2 * dpr;
            ctx.strokeStyle = c.bed;
            ctx.stroke();
        }
    }

    /** Adds the moves in [from, to) whose kind passes the filter. */
    private trace(path: Path2D, from: number, to: number, include: (kind: number) => boolean): void {
        const d = this.data!;
        for (let i = Math.max(from + 1, 1); i < to; i++) {
            if (!include(d.kinds[i])) continue;
            path.moveTo(d.xy[(i - 1) * 2], d.xy[(i - 1) * 2 + 1]);
            path.lineTo(d.xy[i * 2], d.xy[i * 2 + 1]);
        }
    }
}
