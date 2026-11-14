// G-code viewer entry point (imported by Components/Pages/Viewer.razor). One controller owns the
// toolpath and view state; a 2D canvas renderer or a three.js 3D renderer (loaded on first use) draws it.

import { type Bed, type FitTarget, type Renderer, type Toolpath, type ViewOptions, type ViewState, clamp, fetchToolpath, lastPrinted, layerOf } from "./toolpath.js";
import { Viewer2D } from "./viewer-2d.js";

const MODE_KEY = "printdeck.viewer.mode";

export type ViewMode = "2d" | "3d";

/** What Blazor receives after a load (deserialised into Viewer.razor's ToolpathInfo). */
export interface ToolpathInfo {
    layers: number;
    zs: number[];
    kinds: number[];
}

export function create(canvas2d: HTMLCanvasElement, canvas3d: HTMLCanvasElement, bedWidth: number, bedDepth: number): GcodeViewer {
    return new GcodeViewer(canvas2d, canvas3d, { w: bedWidth || 220, d: bedDepth || 220 });
}

export class GcodeViewer {
    private readonly state: ViewState = { layer: 0, fraction: 1, progress: null, opts: { travel: false, previous: true, head: true, hidden: [] } };
    private readonly r2: Viewer2D;
    private r3: Renderer | null = null;
    private data: Toolpath | null = null;
    private mode: ViewMode = "2d";
    private abort: AbortController | null = null;

    constructor(canvas2d: HTMLCanvasElement, private readonly canvas3d: HTMLCanvasElement, private readonly bed: Bed) {
        this.r2 = new Viewer2D(canvas2d, bed, this.state);
        this.r2.active = true;
    }

    storedMode(): ViewMode {
        try { return localStorage.getItem(MODE_KEY) === "3d" ? "3d" : "2d"; } catch { return "2d"; }
    }

    /** Switches renderer. Returns false when 3D is not available (no WebGL 2). */
    async setMode(mode: ViewMode): Promise<boolean> {
        if (mode === "3d" && !this.r3) {
            const { Viewer3D, supported } = await import("./gcode-viewer-3d.js");
            if (!supported()) return false;
            this.r3 = new Viewer3D(this.canvas3d, this.bed, this.state);
            if (this.data) this.r3.setData(this.data);
        }
        this.mode = mode === "3d" ? "3d" : "2d";
        this.r2.active = this.mode === "2d";
        if (this.r3) this.r3.active = this.mode === "3d";
        try { localStorage.setItem(MODE_KEY, this.mode); } catch { /* storage blocked */ }
        this.redraw();
        return true;
    }

    async load(url: string): Promise<ToolpathInfo> {
        this.abort?.abort();
        this.abort = new AbortController();
        const data = await fetchToolpath(url, this.abort.signal);
        this.data = data;
        Object.assign(this.state, { layer: Math.max(0, data.layerCount - 1), fraction: 1, progress: null });
        this.r2.setData(data);
        this.r3?.setData(data);

        const present = new Set<number>(data.kinds);
        present.delete(0);
        return {
            layers: data.layerCount,
            zs: Array.from(data.z, z => Math.round(z * 1000) / 1000),
            kinds: [...present].sort((a, b) => a - b),
        };
    }

    setLayer(layer: number, fraction: number): void {
        this.state.layer = clamp(layer | 0, 0, Math.max(0, (this.data?.layerCount ?? 1) - 1));
        this.state.fraction = clamp(fraction, 0, 1);
        this.redraw();
    }

    /** Marks what a running job has printed. Returns the layer the nozzle is on, or -1. */
    setProgress(bytes: number | null): number {
        this.state.progress = bytes ?? null;
        this.redraw();
        if (this.state.progress == null || !this.data) return -1;
        const i = lastPrinted(this.data, this.state.progress);
        return i < 0 ? 0 : layerOf(this.data, i);
    }

    setOptions(opts: Partial<ViewOptions>): void {
        Object.assign(this.state.opts, opts);
        this.redraw();
    }

    fit(target: FitTarget): void {
        (this.mode === "3d" && this.r3 ? this.r3 : this.r2).fit(target);
    }

    dispose(): void {
        this.abort?.abort();
        this.r2.dispose();
        this.r3?.dispose();
        this.data = null;
    }

    private redraw(): void {
        this.r2.draw();
        this.r3?.draw();
    }
}
