import { Editor, Extension } from '@tiptap/core';
import StarterKit from '@tiptap/starter-kit';
import { BackgroundColor, Color, FontSize, TextStyle } from '@tiptap/extension-text-style';
import Underline from '@tiptap/extension-underline';
import Image from '@tiptap/extension-image';
import Link from '@tiptap/extension-link';
import { TableKit } from '@tiptap/extension-table';
import TaskList from '@tiptap/extension-task-list';
import TaskItem from '@tiptap/extension-task-item';
import Typography from '@tiptap/extension-typography';
import Placeholder from '@tiptap/extension-placeholder';

// The page follows the host theme; the CSS in editor.css only holds the LIGHT fallbacks.
const HOST_HEX = /^#[0-9a-f]{6}$/i;
const MINOR_WORDS = new Set([
    'a', 'an', 'and', 'as', 'at', 'but', 'by', 'for', 'if', 'in', 'is', 'of', 'on', 'or',
    'so', 'the', 'to', 'via', 'per', 'with', 'yet', 'nor',
]);
const CASE_CYCLE = 3;
const MIN_READABLE_CONTRAST = 2.5;
// The blocks that may carry a source's own inline colour / band (dark-site fidelity).
const BLOCK_STYLE_TYPES = [
    'paragraph', 'heading', 'blockquote', 'codeBlock', 'bulletList', 'orderedList',
    'listItem', 'taskItem', 'tableCell', 'tableHeader',
];

const root = document.documentElement;
const params = new URLSearchParams(location.search);

function applyThemeParam(name, value) {
    if (value && HOST_HEX.test(value)) root.style.setProperty(name, value);
}

applyThemeParam('--canvas', params.get('canvas'));
applyThemeParam('--ink', params.get('ink'));
applyThemeParam('--link', params.get('link'));

const colorContext = document.createElement('canvas').getContext('2d');
const rgbCache = new Map();

function parseRgb(value) {
    if (!value) return null;
    if (rgbCache.has(value)) return rgbCache.get(value);
    let rgb = null;
    const keyword = /^(inherit|initial|unset|revert|revert-layer|currentcolor)$/i.test(value);
    if (!keyword && CSS.supports('color', value)) {
        colorContext.fillStyle = '#000000';
        colorContext.fillStyle = value;
        const normalized = colorContext.fillStyle;
        if (/^#[0-9a-f]{6}$/i.test(normalized)) {
            rgb = [1, 3, 5].map(index => Number.parseInt(normalized.slice(index, index + 2), 16));
        } else {
            const parts = normalized.match(/\d+(\.\d+)?/g);
            if (parts && parts.length >= 3 && Number(parts[3] ?? 1) > 0) {
                rgb = parts.slice(0, 3).map(Number);
            }
        }
    }
    rgbCache.set(value, rgb);
    return rgb;
}

function toHex(value) {
    const rgb = parseRgb(value);
    if (!rgb) return '';
    return `#${rgb.map(channel => Math.round(channel).toString(16).padStart(2, '0')).join('')}`.toUpperCase();
}

function relativeLuminance(rgb) {
    const channel = (value) => {
        const unit = value / 255;
        return unit <= 0.03928 ? unit / 12.92 : ((unit + 0.055) / 1.055) ** 2.4;
    };
    return 0.2126 * channel(rgb[0]) + 0.7152 * channel(rgb[1]) + 0.0722 * channel(rgb[2]);
}

function contrastRatio(first, second) {
    const a = relativeLuminance(first);
    const b = relativeLuminance(second);
    return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
}

function readThemeHex(name) {
    return getComputedStyle(root).getPropertyValue(name).trim();
}

const canvasRgb = parseRgb(readThemeHex('--canvas'));
root.style.colorScheme = canvasRgb && relativeLuminance(canvasRgb) <= 0.5 ? 'dark' : 'light';
const inkHex = readThemeHex('--ink');

function post(message) {
    if (!window.chrome?.webview) return;
    window.chrome.webview.postMessage(message);
}

function textRuns(textblock) {
    const runs = [];
    textblock.forEach(child => {
        if (child.type.name === 'hardBreak') runs.push({ text: ' ', marks: child.marks });
        else if (child.isText && child.text) runs.push({ text: child.text, marks: child.marks });
    });
    return runs;
}

function trimRuns(runs) {
    while (runs.length) {
        const text = runs[0].text.replace(/^\s+/, '');
        if (text) {
            runs[0] = { text, marks: runs[0].marks };
            break;
        }
        runs.shift();
    }
    while (runs.length) {
        const last = runs[runs.length - 1];
        const text = last.text.replace(/\s+$/, '');
        if (text) {
            runs[runs.length - 1] = { text, marks: last.marks };
            break;
        }
        runs.pop();
    }
    return runs;
}

function isLetter(character) {
    return /\p{L}/u.test(character);
}

function isWordCharacter(character) {
    return /[\p{L}\p{Nd}'\u2019]/u.test(character);
}

// Mirrors Core\TextCase.cs: every variant maps one char to one char, so marks stay aligned.
function applyCase(text, variant, firstWord) {
    if (variant === 0) return { text: text.toLowerCase(), firstWord };
    if (variant === 1) return { text: text.toUpperCase(), firstWord };
    let result = '';
    let index = 0;
    while (index < text.length) {
        const character = text[index];
        if (!isLetter(character)) {
            if (character === '\n' || character === '\r') firstWord = true;
            result += character;
            index += 1;
            continue;
        }
        const start = index;
        while (index < text.length && isWordCharacter(text[index])) index += 1;
        const word = text.slice(start, index);
        const capitalized = firstWord || !MINOR_WORDS.has(word.toLowerCase());
        result += capitalized
            ? word[0].toUpperCase() + word.slice(1).toLowerCase()
            : word.toLowerCase();
        firstWord = false;
    }
    return { text: result, firstWord };
}

// Global attributes keep a source's block-level colour band and ink ("white-on-dark stays on dark").
const ClipBlockStyle = Extension.create({
    name: 'clipBlockStyle',
    addGlobalAttributes() {
        return [{
            types: BLOCK_STYLE_TYPES,
            attributes: {
                color: {
                    default: null,
                    parseHTML: element => element.style.color || null,
                    renderHTML: attributes => (attributes.color ? { style: `color: ${attributes.color}` } : {}),
                },
                background: {
                    default: null,
                    parseHTML: element => element.style.backgroundColor || null,
                    renderHTML: attributes => (attributes.background ? { style: `background-color: ${attributes.background}` } : {}),
                },
            },
        }];
    },
});

let caseCycle = 0;

const ClipTextCommands = Extension.create({
    name: 'clipTextCommands',
    addCommands() {
        return {
            singleLine: () => ({ state, tr, dispatch }) => {
                const blocks = [];
                state.doc.descendants(node => {
                    if (!node.isTextblock) return true;
                    const runs = trimRuns(textRuns(node));
                    if (runs.length) blocks.push(runs);
                    return false;
                });
                const paragraph = state.schema.nodes.paragraph;
                if (!paragraph) return false;
                const content = [];
                blocks.forEach((runs, blockIndex) => {
                    if (blockIndex) content.push(state.schema.text(' '));
                    runs.forEach(run => content.push(state.schema.text(run.text, run.marks)));
                });
                tr.replaceWith(0, state.doc.content.size, paragraph.create(null, content));
                if (dispatch) dispatch(tr);
                return true;
            },
            cycleCase: () => ({ state, tr, dispatch }) => {
                const selection = state.selection;
                const from = selection.empty ? 0 : selection.from;
                const to = selection.empty ? state.doc.content.size : selection.to;
                const variant = caseCycle;
                const changes = [];
                let firstWord = true;
                state.doc.nodesBetween(from, to, (node, pos) => {
                    if (node.isTextblock) {
                        firstWord = true;
                        return true;
                    }
                    if (!node.isText) return true;
                    const start = Math.max(pos, from);
                    const end = Math.min(pos + node.nodeSize, to);
                    if (start >= end) return true;
                    const text = node.text.slice(start - pos, end - pos);
                    const result = applyCase(text, variant, firstWord);
                    firstWord = result.firstWord;
                    if (result.text !== text) {
                        changes.push({ from: start, to: end, text: result.text, marks: node.marks });
                    }
                    return true;
                });
                if (dispatch) {
                    for (let index = changes.length - 1; index >= 0; index -= 1) {
                        const change = changes[index];
                        tr.replaceWith(change.from, change.to, state.schema.text(change.text, change.marks));
                    }
                    dispatch(tr);
                    caseCycle = (caseCycle + 1) % CASE_CYCLE;
                }
                return true;
            },
        };
    },
});

let baseline = '';
let dirty = false;
let loading = false;

const editor = new Editor({
    element: document.getElementById('editor'),
    content: '',
    extensions: [
        StarterKit.configure({ link: false, underline: false }),
        Underline,
        TextStyle,
        Color,
        FontSize,
        BackgroundColor,
        Link.configure({ openOnClick: false, autolink: true, linkOnPaste: true }),
        Image.configure({ allowBase64: true, inline: false }),
        TaskList,
        TaskItem.configure({ nested: true }),
        TableKit.configure({ table: { resizable: false, renderWrapper: true } }),
        Typography,
        Placeholder.configure({ placeholder: '' }),
        ClipBlockStyle,
        ClipTextCommands,
    ],
    onUpdate: () => {
        syncDirty();
        postSelection();
    },
    onSelectionUpdate: () => postSelection(),
});

function syncDirty() {
    if (loading) return;
    const next = editor.getHTML() !== baseline;
    if (next === dirty) return;
    dirty = next;
    post({ type: 'dirty', dirty });
}

function selectionState() {
    const attributes = editor.getAttributes('textStyle');
    const size = Number.parseFloat(attributes.fontSize);
    return {
        type: 'sel',
        bold: editor.isActive('bold'),
        italic: editor.isActive('italic'),
        underline: editor.isActive('underline'),
        strike: editor.isActive('strike'),
        size: Number.isFinite(size) ? size : 0,
        color: toHex(attributes.color),
        highlight: toHex(attributes.backgroundColor),
    };
}

function postSelection() {
    post(selectionState());
}

function walkText(node, pos, insideBackground, visit) {
    if (node.isText) {
        visit(node, pos, insideBackground);
        return;
    }
    const backgrounded = insideBackground || Boolean(node.attrs && node.attrs.background);
    node.forEach((child, offset) => walkText(child, pos + 1 + offset, backgrounded, visit));
}

// Mirrors RichRow's dark-pill rule, opposite mechanism: a run nobody can read on the canvas gets the theme ink.
function guardContrast() {
    const markType = editor.schema.marks.textStyle;
    if (!markType || !canvasRgb) return;
    const tr = editor.state.tr;
    let changed = false;
    editor.state.doc.forEach((child, offset) => walkText(child, offset, false, (node, pos, insideBackground) => {
        if (insideBackground) return;
        const textStyle = node.marks.find(mark => mark.type === markType);
        if (!textStyle || textStyle.attrs.backgroundColor || !textStyle.attrs.color) return;
        const rgb = parseRgb(textStyle.attrs.color);
        if (!rgb || contrastRatio(rgb, canvasRgb) >= MIN_READABLE_CONTRAST) return;
        tr.removeMark(pos, pos + node.nodeSize, markType);
        tr.addMark(pos, pos + node.nodeSize, markType.create({ ...textStyle.attrs, color: inkHex }));
        changed = true;
    }));
    // A block's own ink (a <p style="color:…"> with no band) is just as invisible, and it is not a mark.
    editor.state.doc.descendants((node, pos, parent) => {
        if (!node.isTextblock || !node.attrs.color || node.attrs.background) return true;
        if (parent && parent.attrs && parent.attrs.background) return true;
        const rgb = parseRgb(node.attrs.color);
        if (!rgb || contrastRatio(rgb, canvasRgb) >= MIN_READABLE_CONTRAST) return true;
        tr.setNodeMarkup(pos, undefined, { ...node.attrs, color: inkHex });
        changed = true;
        return true;
    });
    if (!changed) return;
    tr.setMeta('addToHistory', false);
    editor.view.dispatch(tr);
}

function beginLoad() {
    loading = true;
}

function endLoad() {
    baseline = editor.getHTML();
    loading = false;
    syncDirty();
}

function load(html) {
    beginLoad();
    editor.commands.setContent(html || '', { emitUpdate: false });
    guardContrast();
    editor.commands.focus('end');
    endLoad();
}

function loadPlain(text) {
    beginLoad();
    const lines = String(text ?? '').split(/\r?\n/);
    editor.commands.setContent({
        type: 'doc',
        content: lines.map(line => (line
            ? { type: 'paragraph', content: [{ type: 'text', text: line }] }
            : { type: 'paragraph' })),
    }, { emitUpdate: false });
    editor.commands.focus('end');
    endLoad();
}

function getHtml() {
    return editor.getHTML();
}

function getText() {
    return editor.getText({ blockSeparator: '\n' });
}

function run(cmd, arg) {
    editor.commands.focus();
    let ok = false;
    switch (cmd) {
        case 'bold':
            ok = editor.chain().toggleBold().run();
            break;
        case 'italic':
            ok = editor.chain().toggleItalic().run();
            break;
        case 'underline':
            ok = editor.chain().toggleUnderline().run();
            break;
        case 'strike':
            ok = editor.chain().toggleStrike().run();
            break;
        case 'color':
            ok = editor.chain().setColor(arg).run();
            break;
        case 'highlight': {
            const color = arg ? arg.color : null;
            const ink = arg ? arg.ink : null;
            let chain = editor.chain();
            chain = color ? chain.setBackgroundColor(color) : chain.unsetBackgroundColor();
            if (ink) chain = chain.setColor(ink);
            ok = chain.run();
            break;
        }
        case 'size':
            ok = editor.chain().setFontSize(`${arg}pt`).run();
            break;
        case 'clearFormat':
            ok = editor.chain().unsetAllMarks().run();
            break;
        case 'singleLine':
            ok = editor.commands.singleLine();
            break;
        case 'case':
            ok = editor.commands.cycleCase();
            break;
        default:
            ok = false;
    }
    postSelection();
    return ok;
}

function focusEditor() {
    editor.commands.focus();
}

document.addEventListener('keydown', event => {
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') {
        event.preventDefault();
        post({ type: 'save' });
        return;
    }
    if (event.key === 'Escape' || event.key === 'F2') {
        event.preventDefault();
        post({ type: 'cancel' });
    }
}, true);

window.clipBridge = {
    load,
    loadPlain,
    getHtml,
    getText,
    run,
    focus: focusEditor,
};

post({ type: 'ready' });
