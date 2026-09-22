const STORAGE_KEY = 'naval-dot-net:sound';

let context = null;
let master = null;
let noise = null;
let ambience = null;
let bubbleTimer = 0;
let muted = false;

function storedMute() {
    try {
        return localStorage.getItem(STORAGE_KEY) === 'off';
    } catch {
        return false;
    }
}

function remember(value) {
    try {
        localStorage.setItem(STORAGE_KEY, value ? 'off' : 'on');
    } catch {
        /* navigation privée : le réglage ne survit pas à l'onglet, le son fonctionne quand même */
    }
}

function ensure() {
    if (context) {
        return context;
    }

    const Ctor = window.AudioContext || window.webkitAudioContext;

    if (!Ctor) {
        return null;
    }

    context = new Ctor();
    master = context.createGain();
    master.gain.value = muted ? 0 : 0.55;
    master.connect(context.destination);
    return context;
}

function noiseBuffer() {
    if (noise) {
        return noise;
    }

    const length = Math.floor(context.sampleRate * 2);
    noise = context.createBuffer(1, length, context.sampleRate);
    const data = noise.getChannelData(0);

    for (let i = 0; i < length; i++) {
        data[i] = Math.random() * 2 - 1;
    }

    return noise;
}

// exponentialRamp refuse zéro : l'enveloppe part et revient vers un epsilon,
// jamais vers 0, sinon la rampe lève et la voix ne sonne pas du tout.
function envelope(start, peak, attack, release) {
    const gain = context.createGain();
    gain.gain.setValueAtTime(0.0001, start);
    gain.gain.exponentialRampToValueAtTime(peak, start + attack);
    gain.gain.exponentialRampToValueAtTime(0.0001, start + attack + release);
    gain.connect(master);
    return gain;
}

function bloop(start, from, to, peak, length) {
    const osc = context.createOscillator();
    osc.type = 'sine';
    osc.frequency.setValueAtTime(from, start);
    osc.frequency.exponentialRampToValueAtTime(to, start + length);
    osc.connect(envelope(start, peak, 0.012, length));
    osc.start(start);
    osc.stop(start + length + 0.1);
}

function hiss(start, from, to, peak, length, q = 1.4) {
    const source = context.createBufferSource();
    source.buffer = noiseBuffer();

    const filter = context.createBiquadFilter();
    filter.type = 'bandpass';
    filter.Q.value = q;
    filter.frequency.setValueAtTime(from, start);
    filter.frequency.exponentialRampToValueAtTime(to, start + length);

    source.connect(filter).connect(envelope(start, peak, 0.02, length));
    source.start(start);
    source.stop(start + length + 0.1);
}

function rumble(start, peak, length) {
    const osc = context.createOscillator();
    osc.type = 'sine';
    osc.frequency.setValueAtTime(170, start);
    osc.frequency.exponentialRampToValueAtTime(42, start + length);
    osc.connect(envelope(start, peak, 0.01, length));
    osc.start(start);
    osc.stop(start + length + 0.1);

    const source = context.createBufferSource();
    source.buffer = noiseBuffer();
    const filter = context.createBiquadFilter();
    filter.type = 'lowpass';
    filter.frequency.setValueAtTime(1400, start);
    filter.frequency.exponentialRampToValueAtTime(180, start + length * 0.7);
    source.connect(filter).connect(envelope(start, peak * 0.7, 0.008, length * 0.7));
    source.start(start);
    source.stop(start + length + 0.1);
}

function chime(start, notes, step, peak) {
    notes.forEach((frequency, index) => {
        const at = start + index * step;
        const osc = context.createOscillator();
        osc.type = 'triangle';
        osc.frequency.setValueAtTime(frequency, at);
        osc.connect(envelope(at, peak, 0.02, step * 2.2));
        osc.start(at);
        osc.stop(at + step * 2.4);
    });
}

const voices = {
    bubble: start => bloop(start, 260, 900, 0.45, 0.12),

    tap: start => bloop(start, 380, 700, 0.18, 0.07),

    dive: start => {
        hiss(start, 300, 1800, 0.35, 0.5, 0.8);
        bloop(start + 0.1, 180, 420, 0.4, 0.3);
        bloop(start + 0.3, 220, 760, 0.35, 0.2);
    },

    drop: start => bloop(start, 520, 190, 0.4, 0.14),

    miss: start => {
        hiss(start, 2600, 700, 0.22, 0.14, 2.5);
        hiss(start + 0.12, 1900, 280, 0.5, 0.35, 1.1);
        bloop(start + 0.16, 900, 300, 0.3, 0.14);
    },

    hit: start => {
        hiss(start, 2400, 800, 0.2, 0.12, 2.5);
        rumble(start + 0.1, 0.85, 0.5);
        bloop(start + 0.18, 620, 180, 0.3, 0.2);
    },

    sunk: start => {
        hiss(start, 2400, 800, 0.2, 0.12, 2.5);
        rumble(start + 0.1, 0.95, 0.75);

        const glide = context.createOscillator();
        glide.type = 'sawtooth';
        glide.frequency.setValueAtTime(300, start + 0.2);
        glide.frequency.exponentialRampToValueAtTime(62, start + 1.05);

        const shaped = context.createBiquadFilter();
        shaped.type = 'lowpass';
        shaped.frequency.value = 900;

        glide.connect(shaped).connect(envelope(start + 0.2, 0.3, 0.06, 0.85));
        glide.start(start + 0.2);
        glide.stop(start + 1.2);

        [0.35, 0.55, 0.8].forEach((offset, index) =>
            bloop(start + offset, 200 + index * 60, 700 + index * 90, 0.22, 0.13));
    },

    win: start => {
        chime(start, [523.25, 659.25, 783.99, 1046.5], 0.13, 0.4);
        [0.1, 0.26, 0.42, 0.58].forEach(offset => bloop(start + offset, 300, 1000, 0.2, 0.11));
    },

    lose: start => {
        chime(start, [440, 392, 329.63, 246.94], 0.2, 0.34);
        rumble(start + 0.5, 0.4, 0.9);
    }
};

function scheduleAmbientBubble() {
    bubbleTimer = window.setTimeout(() => {
        if (context && !muted && context.state === 'running') {
            bloop(context.currentTime, 180 + Math.random() * 260, 500 + Math.random() * 500, 0.06, 0.16);
        }

        scheduleAmbientBubble();
    }, 2200 + Math.random() * 4500);
}

function startAmbience() {
    if (ambience || !context) {
        return;
    }

    const source = context.createBufferSource();
    source.buffer = noiseBuffer();
    source.loop = true;

    const filter = context.createBiquadFilter();
    filter.type = 'lowpass';
    filter.frequency.value = 240;

    const gain = context.createGain();
    gain.gain.value = 0.05;

    // Le courant : une LFO très lente ouvre et referme le filtre, sinon le
    // bruit de fond s'entend comme un souffle fixe et devient pénible.
    const sway = context.createOscillator();
    sway.type = 'sine';
    sway.frequency.value = 0.06;
    const swayDepth = context.createGain();
    swayDepth.gain.value = 90;
    sway.connect(swayDepth).connect(filter.frequency);
    sway.start();

    source.connect(filter).connect(gain).connect(master);
    source.start();

    ambience = { source, sway };
    scheduleAmbientBubble();
}

export function prime() {
    muted = storedMute();

    if (!ensure()) {
        return { available: false, muted };
    }

    // Politique d'autoplay : un contexte créé hors geste utilisateur naît
    // suspendu. On le réveille au premier geste, quel qu'il soit.
    const wake = () => {
        context.resume().then(startAmbience).catch(() => { });
    };

    if (context.state === 'running') {
        startAmbience();
    } else {
        window.addEventListener('pointerdown', wake, { once: true });
        window.addEventListener('keydown', wake, { once: true });
    }

    return { available: true, muted };
}

export function play(name, delay) {
    const voice = voices[name];

    if (!voice || muted || !ensure()) {
        return;
    }

    if (context.state === 'suspended') {
        context.resume().then(startAmbience).catch(() => { });
    }

    voice(context.currentTime + Math.max(0, delay || 0) + 0.02);
}

export function setMuted(value) {
    muted = !!value;
    remember(muted);

    if (!context) {
        return muted;
    }

    master.gain.cancelScheduledValues(context.currentTime);
    master.gain.setTargetAtTime(muted ? 0 : 0.55, context.currentTime, 0.05);

    if (!muted) {
        context.resume().then(startAmbience).catch(() => { });
    }

    return muted;
}

export function stop() {
    window.clearTimeout(bubbleTimer);

    if (ambience) {
        ambience.source.stop();
        ambience.sway.stop();
        ambience = null;
    }
}
