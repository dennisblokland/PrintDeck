import { describe, expect, it } from "vitest";

import { jogKeyFor } from "./keyboard-jog.js";

describe("jogKeyFor", () => {
    it.each([
        ["ArrowLeft", "X-"],
        ["ArrowRight", "X+"],
        ["ArrowUp", "Y+"],
        ["ArrowDown", "Y-"],
        ["PageUp", "Z+"],
        ["PageDown", "Z-"],
        ["Home", "home-xy"],
        ["End", "home-z"],
    ])("maps %s to %s", (key, expected) => {
        expect(jogKeyFor({ key }, 4)).toBe(expected);
    });

    it("lets arrows repeat while held", () => {
        expect(jogKeyFor({ key: "ArrowUp", repeat: true }, 4)).toBe("Y+");
    });

    it("picks a step size with the number keys", () => {
        expect(jogKeyFor({ key: "1" }, 4)).toBe("step-0");
        expect(jogKeyFor({ key: "4" }, 4)).toBe("step-3");
        expect(jogKeyFor({ key: "5" }, 4)).toBeNull();
        expect(jogKeyFor({ key: "2", repeat: true }, 4)).toBeNull();
    });

    it("leaves keys with Ctrl, Alt or Meta to the browser", () => {
        expect(jogKeyFor({ key: "ArrowLeft", ctrlKey: true }, 4)).toBeNull();
        expect(jogKeyFor({ key: "ArrowLeft", altKey: true }, 4)).toBeNull();
        expect(jogKeyFor({ key: "Home", metaKey: true }, 4)).toBeNull();
    });

    it("ignores typing in fields", () => {
        expect(jogKeyFor({ key: "ArrowLeft", targetTag: "INPUT" }, 4)).toBeNull();
        expect(jogKeyFor({ key: "1", targetTag: "TEXTAREA" }, 4)).toBeNull();
        expect(jogKeyFor({ key: "PageUp", targetTag: "SELECT" }, 4)).toBeNull();
        expect(jogKeyFor({ key: "End", targetTag: "DIV", targetEditable: true }, 4)).toBeNull();
        expect(jogKeyFor({ key: "ArrowLeft", targetTag: "BUTTON" }, 4)).toBe("X-");
    });

    it("ignores other keys", () => {
        expect(jogKeyFor({ key: "a" }, 4)).toBeNull();
        expect(jogKeyFor({ key: "Enter" }, 4)).toBeNull();
    });
});
