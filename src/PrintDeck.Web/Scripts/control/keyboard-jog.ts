/**
 * Keyboard jogging for the Control page:
 * arrows move X/Y, Page Up/Down move Z, Home homes X and Y, End homes Z, and 1–4 pick the step size.
 * Keys typed into a field, or pressed with Ctrl, Alt or Meta, are left alone.
 */

/** What a key asks for, as sent to the page: "X+", "Y-", "Z+", "home-xy", "home-z", "step-0" … "step-3". */
export type JogKey = "X+" | "X-" | "Y+" | "Y-" | "Z+" | "Z-" | "home-xy" | "home-z" | `step-${number}`;

/** The parts of a KeyboardEvent the mapping looks at, so it can be tested without a DOM. */
export interface KeyPress {
    key: string;
    ctrlKey?: boolean;
    altKey?: boolean;
    metaKey?: boolean;
    repeat?: boolean;
    /** Tag name of the focused element ("INPUT", "TEXTAREA", …), upper case. */
    targetTag?: string;
    targetEditable?: boolean;
}

const keys: Record<string, JogKey> = {
    ArrowLeft: "X-",
    ArrowRight: "X+",
    ArrowUp: "Y+",
    ArrowDown: "Y-",
    PageUp: "Z+",
    PageDown: "Z-",
    Home: "home-xy",
    End: "home-z",
};

const typing = new Set(["INPUT", "TEXTAREA", "SELECT"]);

export function jogKeyFor(press: KeyPress, stepCount: number): JogKey | null {
    if (press.ctrlKey || press.altKey || press.metaKey) return null;
    if (press.targetEditable || (press.targetTag !== undefined && typing.has(press.targetTag))) return null;

    const mapped = keys[press.key];
    if (mapped !== undefined) return mapped;

    // Number keys pick a step size; holding one down repeats nothing useful.
    if (!press.repeat && /^[1-9]$/.test(press.key)) {
        const index = Number(press.key) - 1;
        if (index < stepCount) return `step-${index}`;
    }
    return null;
}

/** The .NET side: the Control page's [JSInvokable] method. */
export interface JogTarget {
    invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

export interface KeyboardJog {
    dispose(): void;
}

/** Listens on the whole document until disposed; handled keys don't scroll the page. */
export function attach(target: JogTarget, stepCount: number): KeyboardJog {
    const onKeyDown = (e: KeyboardEvent): void => {
        const element = e.target instanceof HTMLElement ? e.target : null;
        const key = jogKeyFor({
            key: e.key,
            ctrlKey: e.ctrlKey,
            altKey: e.altKey,
            metaKey: e.metaKey,
            repeat: e.repeat,
            targetTag: element?.tagName,
            targetEditable: element?.isContentEditable,
        }, stepCount);
        if (key === null) return;

        e.preventDefault();
        target.invokeMethodAsync("OnJogKey", key).catch(() => { /* page already gone */ });
    };

    document.addEventListener("keydown", onKeyDown);
    return { dispose: () => document.removeEventListener("keydown", onKeyDown) };
}
