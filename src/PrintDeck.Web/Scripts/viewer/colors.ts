// Reads the viewer palette from CSS custom properties (--tp-*, see app.css).

export type Rgba = [r: number, g: number, b: number, a: number];

/** CSS custom property value on an element, with a fallback when unset. */
export function cssVar(el: Element, name: string): string {
    return getComputedStyle(el).getPropertyValue(name).trim() || "#888";
}

let probe: CanvasRenderingContext2D | null = null;

/** Any CSS colour (including `rgb(r g b / a)`) → sRGB [r, g, b, a] in 0..1. */
export function rgba(color: string): Rgba {
    probe ??= document.createElement("canvas").getContext("2d", { willReadFrequently: true })!;
    probe.clearRect(0, 0, 1, 1);
    probe.fillStyle = "#000";
    probe.fillStyle = color;
    probe.fillRect(0, 0, 1, 1);
    const [r, g, b, a] = probe.getImageData(0, 0, 1, 1).data;
    return [r / 255, g / 255, b / 255, a / 255];
}

/** Translucent colour composited onto an opaque one. */
export function over(fg: Rgba, bg: Rgba): Rgba {
    const mix = (i: number) => fg[i] * fg[3] + bg[i] * (1 - fg[3]);
    return [mix(0), mix(1), mix(2), 1];
}
