// Toolpath data shared by the 2D and 3D viewers. Binary format: see Api/ToolpathApi.cs.

/** 0 = travel, 1..7 = ToolpathKind (outer wall … other). */
export const KINDS = 8;
export const KIND_OTHER = 7;

export interface Bounds {
    minX: number;
    minY: number;
    maxX: number;
    maxY: number;
}

export interface Toolpath {
    layerCount: number;
    /** All extrusion points, including purge lines. */
    bounds: Bounds;
    /** Points with a slicer feature type, or null when the file has none. */
    modelBounds: Bounds | null;
    /** Per layer: Z, first point and point count. */
    z: Float32Array;
    start: Uint32Array;
    count: Uint32Array;
    /** Per point: XY in mm, file byte offset of its move, ToolpathKind of the move ending here. */
    xy: Float32Array;
    offsets: Uint32Array;
    kinds: Uint8Array;
}

export interface ViewOptions {
    travel: boolean;
    previous: boolean;
    /** Show a nozzle model at the nozzle position. */
    head: boolean;
    hidden: number[];
}

/** View state owned by the controller and read by whichever renderer is active. */
export interface ViewState {
    layer: number;
    /** Share of the selected layer's moves to show, 0..1. */
    fraction: number;
    /** Bytes of the file the running job has read, or null when this file is not printing. */
    progress: number | null;
    opts: ViewOptions;
}

export interface Bed {
    w: number;
    d: number;
}

export type FitTarget = "model" | "bed";

export interface Renderer {
    active: boolean;
    setData(data: Toolpath): void;
    fit(target: FitTarget): void;
    draw(): void;
    dispose(): void;
}

export async function fetchToolpath(url: string, signal?: AbortSignal): Promise<Toolpath> {
    const res = await fetch(url, { signal });
    if (!res.ok) throw new Error(res.status === 404 ? "File not found." : `Could not read toolpath (${res.status}).`);
    return parse(await res.arrayBuffer());
}

export function parse(buffer: ArrayBuffer): Toolpath {
    const v = new DataView(buffer);
    const magic = String.fromCharCode(v.getUint8(0), v.getUint8(1), v.getUint8(2), v.getUint8(3));
    if (magic !== "PDTP" || v.getUint32(4, true) !== 1) throw new Error("Unsupported toolpath format.");
    const layerCount = v.getUint32(8, true);
    const points = v.getUint32(12, true);
    const bounds: Bounds = { minX: v.getFloat32(16, true), minY: v.getFloat32(20, true), maxX: v.getFloat32(24, true), maxY: v.getFloat32(28, true) };

    const z = new Float32Array(layerCount), start = new Uint32Array(layerCount), count = new Uint32Array(layerCount);
    let p = 40;
    for (let i = 0; i < layerCount; i++, p += 12) {
        z[i] = v.getFloat32(p, true);
        start[i] = v.getUint32(p + 4, true);
        count[i] = v.getUint32(p + 8, true);
    }
    const xy = new Float32Array(buffer, p, points * 2); p += points * 8;
    const offsets = new Uint32Array(buffer, p, points); p += points * 4;
    const kinds = new Uint8Array(buffer, p, points);
    return { layerCount, bounds, modelBounds: typedBounds(xy, kinds), z, start, count, xy, offsets, kinds };
}

/** Bounds of moves with a slicer feature type, so "fit model" skips purge lines in start G-code. */
function typedBounds(xy: Float32Array, kinds: Uint8Array): Bounds | null {
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (let i = 1; i < kinds.length; i++) {
        const k = kinds[i];
        if (k === 0 || k === KIND_OTHER) continue;
        for (const j of [i - 1, i]) {
            const x = xy[j * 2], y = xy[j * 2 + 1];
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }
    }
    return maxX > minX ? { minX, minY, maxX, maxY } : null;
}

/** Index of the last point whose move the job has read (offsets are non-decreasing), or -1. */
export function lastPrinted(d: Toolpath, bytes: number): number {
    let lo = 0, hi = d.offsets.length - 1, ans = -1;
    while (lo <= hi) {
        const mid = (lo + hi) >> 1;
        if (d.offsets[mid] <= bytes) { ans = mid; lo = mid + 1; } else hi = mid - 1;
    }
    return ans;
}

/** Layer that contains a point. */
export function layerOf(d: Toolpath, point: number): number {
    return Math.max(lowerBound(d.start, point + 1) - 1, 0);
}

/** First index whose value is >= v (array sorted ascending). */
export function lowerBound(arr: ArrayLike<number>, v: number): number {
    let lo = 0, hi = arr.length;
    while (lo < hi) {
        const mid = (lo + hi) >> 1;
        if (arr[mid] < v) lo = mid + 1; else hi = mid;
    }
    return lo;
}

/**
 * Point the nozzle is at: the job's last read move, or the end of the scrubbed range when the move
 * slider is not at the end or the print head is shown. -1 when there is nothing to mark.
 */
export function nozzlePoint(d: Toolpath, s: ViewState): number {
    if (s.progress != null) return lastPrinted(d, s.progress);
    if (d.layerCount === 0) return -1;
    const layer = clamp(s.layer, 0, d.layerCount - 1);
    const start = d.start[layer];
    const cut = start + Math.round(d.count[layer] * s.fraction);
    return (s.fraction < 1 || s.opts.head) && cut > start ? cut - 1 : -1;
}

export const clamp = (v: number, lo: number, hi: number): number => Math.min(hi, Math.max(lo, v));
