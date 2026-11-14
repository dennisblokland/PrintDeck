import { afterEach, describe, expect, it, vi } from "vitest";

import { type Toolpath, type ViewState, clamp, fetchToolpath, lastPrinted, layerOf, lowerBound, nozzlePoint, parse } from "./toolpath.js";

interface LayerSpec {
    z: number;
    /** [x, y, byte offset, kind] — kind 0 = travel to the point. */
    points: [number, number, number, number][];
}

/** Encodes layers the way Api/ToolpathApi.cs does. */
function encode(layers: LayerSpec[], bounds = [0, 0, 100, 100], version = 1): ArrayBuffer {
    const points = layers.flatMap(l => l.points);
    const n = points.length;
    const buf = new ArrayBuffer(40 + layers.length * 12 + n * 8 + n * 4 + n);
    const v = new DataView(buf);
    "PDTP".split("").forEach((c, i) => v.setUint8(i, c.charCodeAt(0)));
    v.setUint32(4, version, true);
    v.setUint32(8, layers.length, true);
    v.setUint32(12, n, true);
    bounds.forEach((b, i) => v.setFloat32(16 + i * 4, b, true));
    v.setFloat64(32, 1234, true);
    let p = 40, start = 0;
    for (const l of layers) {
        v.setFloat32(p, l.z, true);
        v.setUint32(p + 4, start, true);
        v.setUint32(p + 8, l.points.length, true);
        p += 12;
        start += l.points.length;
    }
    points.forEach(([x, y], i) => { v.setFloat32(p + i * 8, x, true); v.setFloat32(p + i * 8 + 4, y, true); });
    p += n * 8;
    points.forEach(([, , offset], i) => v.setUint32(p + i * 4, offset, true));
    p += n * 4;
    points.forEach(([, , , kind], i) => v.setUint8(p + i, kind));
    return buf;
}

// Layer 0: purge line (kind 7) then an outer wall square; layer 1: infill line.
const sample = (): Toolpath => parse(encode([
    {
        z: 0.2,
        points: [
            [0, -3, 10, 0], [60, -3, 20, 7],                                      // purge line
            [40, 40, 30, 0], [60, 40, 40, 1], [60, 60, 50, 1], [40, 60, 60, 1], // walls
        ],
    },
    { z: 0.4, points: [[40, 40, 70, 0], [60, 60, 80, 3]] },
], [0, -3, 60, 60]));

const view = (over: Partial<ViewState> = {}): ViewState => ({
    layer: 0, fraction: 1, progress: null, opts: { travel: false, previous: true, head: false, hidden: [] }, ...over,
});

describe("parse", () => {
    it("reads layers and point arrays", () => {
        const d = sample();
        expect(d.layerCount).toBe(2);
        expect(Array.from(d.z)).toEqual([expect.closeTo(0.2), expect.closeTo(0.4)]);
        expect(Array.from(d.start)).toEqual([0, 6]);
        expect(Array.from(d.count)).toEqual([6, 2]);
        expect(Array.from(d.xy.slice(6, 8))).toEqual([60, 40]);
        expect(Array.from(d.offsets)).toEqual([10, 20, 30, 40, 50, 60, 70, 80]);
        expect(Array.from(d.kinds)).toEqual([0, 7, 0, 1, 1, 1, 0, 3]);
        expect(d.bounds).toEqual({ minX: 0, minY: -3, maxX: 60, maxY: 60 });
    });

    it("leaves untyped moves (purge lines) out of the model bounds", () => {
        expect(sample().modelBounds).toEqual({ minX: 40, minY: 40, maxX: 60, maxY: 60 });
    });

    it("has no model bounds when the slicer wrote no feature types", () => {
        const d = parse(encode([{ z: 0.2, points: [[0, 0, 1, 0], [10, 0, 2, 7]] }]));
        expect(d.modelBounds).toBeNull();
    });

    it("rejects other formats", () => {
        expect(() => parse(encode([], undefined, 2))).toThrow(/Unsupported/);
        expect(() => parse(new ArrayBuffer(40))).toThrow(/Unsupported/);
    });
});

describe("lastPrinted", () => {
    const d = sample();
    it.each([
        [0, -1],    // nothing read yet
        [10, 0],
        [45, 3],    // between offsets → last one at or below
        [50, 4],
        [1e9, 7],
    ])("bytes %d → point %d", (bytes, point) => expect(lastPrinted(d, bytes)).toBe(point));
});

describe("layerOf", () => {
    const d = sample();
    it.each([[0, 0], [5, 0], [6, 1], [7, 1]])("point %d → layer %d", (point, layer) => expect(layerOf(d, point)).toBe(layer));
});

describe("lowerBound", () => {
    it.each([[0, 0], [1, 0], [2, 1], [5, 2], [6, 3], [99, 4]])("first index >= %d is %d", (v, i) =>
        expect(lowerBound([1, 3, 5, 7], v)).toBe(i));
    it("handles empty arrays", () => expect(lowerBound([], 3)).toBe(0));
});

describe("nozzlePoint", () => {
    const d = sample();

    it("follows the job when it is printing this file", () => {
        expect(nozzlePoint(d, view({ progress: 55 }))).toBe(4);
        expect(nozzlePoint(d, view({ progress: 55, layer: 1, fraction: 0.5 }))).toBe(4); // scrubber ignored
    });

    it("marks the end of the scrubbed range", () => {
        expect(nozzlePoint(d, view({ fraction: 0.5 }))).toBe(2);
    });

    it("hides the marker at the end of a layer unless the print head is shown", () => {
        expect(nozzlePoint(d, view())).toBe(-1);
        expect(nozzlePoint(d, view({ opts: { ...view().opts, head: true } }))).toBe(5);
        expect(nozzlePoint(d, view({ layer: 1, opts: { ...view().opts, head: true } }))).toBe(7);
    });

    it("has nothing to mark at the very start of a layer", () => {
        expect(nozzlePoint(d, view({ fraction: 0 }))).toBe(-1);
    });

    it("clamps an out-of-range layer", () => {
        expect(nozzlePoint(d, view({ layer: 9, opts: { ...view().opts, head: true } }))).toBe(7);
    });
});

describe("clamp", () => {
    it("keeps values in range", () => {
        expect(clamp(-1, 0, 5)).toBe(0);
        expect(clamp(3, 0, 5)).toBe(3);
        expect(clamp(9, 0, 5)).toBe(5);
    });
});

describe("fetchToolpath", () => {
    afterEach(() => { vi.unstubAllGlobals(); });

    const respond = (body: BodyInit | null, status = 200) =>
        vi.stubGlobal("fetch", vi.fn(async () => new Response(body, { status })));

    it("parses the response body", async () => {
        respond(encode([{ z: 0.2, points: [[0, 0, 1, 0], [10, 0, 2, 1]] }]));
        const d = await fetchToolpath("/toolpath/a.gcode");
        expect(d.layerCount).toBe(1);
        expect(Array.from(d.kinds)).toEqual([0, 1]);
    });

    it("passes the abort signal on", async () => {
        respond(encode([]));
        const signal = new AbortController().signal;
        await fetchToolpath("/toolpath/a.gcode", signal);
        expect(fetch).toHaveBeenCalledWith("/toolpath/a.gcode", { signal });
    });

    it("explains a missing file", async () => {
        respond(null, 404);
        await expect(fetchToolpath("/toolpath/gone.gcode")).rejects.toThrow("File not found.");
    });

    it("reports other errors with their status", async () => {
        respond(null, 500);
        await expect(fetchToolpath("/toolpath/a.gcode")).rejects.toThrow("Could not read toolpath (500).");
    });
});
