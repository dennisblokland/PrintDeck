import { describe, expect, it } from "vitest";

import { over } from "./colors.js";

describe("over", () => {
    it("composites a translucent colour onto an opaque one", () => {
        const c = over([1, 1, 1, 0.25], [0, 0.2, 0.4, 1]);
        expect(c[0]).toBeCloseTo(0.25);
        expect(c[1]).toBeCloseTo(0.4);
        expect(c[2]).toBeCloseTo(0.55);
        expect(c[3]).toBe(1);
    });

    it("keeps an opaque foreground", () => {
        expect(over([0.1, 0.2, 0.3, 1], [1, 1, 1, 1])).toEqual([0.1, 0.2, 0.3, 1]);
    });

    it("shows only the background through a transparent foreground", () => {
        expect(over([1, 0, 0, 0], [0.1, 0.2, 0.3, 1])).toEqual([0.1, 0.2, 0.3, 1]);
    });
});
