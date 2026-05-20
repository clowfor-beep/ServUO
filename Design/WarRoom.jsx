import { useState, useRef, useEffect } from "react";

const SHARD_DOC = `# UO Shard Design Document
## Emulator: ServUO (C# / .NET) | Client: ClassicUO

## Vision
A heavily quest-driven, content-rich Ultima Online private server featuring:
- Automated world events running 24/7 (invasions, champion spawns, world bosses)
- Three-tier risk/reward zone system
- Custom skill combination templates that unlock unique abilities

## World Structure
### Tier 1 — Safe Lands (Trammel-style)
PvE focused, quest hubs, crafting, learning areas.
Auto events: seasonal invasions, town raids, traveling merchants.

### Tier 2 — Contested Zones
PvP enabled, better loot tables, champion spawn system.
Auto events: Champion Spawns (escalating boss waves), capture points, guild warfare.

### Tier 3 — Cursed Lands (High Risk / High Reward)
No resurrection protection, loot drops on death.
Auto events: World bosses, dungeon lockdowns, blood moon events.
Rare artifacts only found here.

## Skill Template System
Players combine skills to unlock unique class abilities:
- "Spellblade": Swords 80+ AND Magery 80+ AND Meditation 60+ → Arcane Strike, Mana Leech on hit
- "Shadowdancer": Stealth 80+ AND Fencing 80+ AND Ninjitsu 60+ → Shadow Step, Ambush Crit multiplier
- "Warlord": Tactics 100 AND Swords 100 AND Chivalry 80+ → Battle Cry (AoE fear), Last Stand
- "Beastlord": Taming 80+ AND Animal Lore 80+ AND Magery 60+ → Pack Bond (buff all tames), War Howl

## Tech Stack
Emulator: ServUO (C# / .NET)
Client: ClassicUO (open source)
Scripting: C# for all custom systems
Config: XML files for quests, events, loot tables
Hosting: Linux VPS (Ubuntu)

## Build Order
1. Get ServUO running locally, test with ClassicUO
2. Configure world facets — set up the 3 risk tiers
3. Enable Champion Spawns + Invasions — auto-events working
4. Build the TemplateManager — skill combo → ability unlock system
5. Write quest chains — story + daily + dynamic quests
6. Tune loot tables per risk tier
7. Deploy to VPS and open to friends`;

const PERSONAS = [
  {
    id: "lead",
    name: "Project Lead",
    short: "Lead",
    icon: "⚜️",
    accent: "#c9952a",
    glow: "rgba(201,149,42,0.35)",
    border: "rgba(201,149,42,0.4)",
    greeting: "The war table is set. What shall we plan today, commander?",
    system: `You are the project lead and coordinator for this Ultima Online private shard development. You help prioritize tasks, create roadmaps, break features into milestones, identify blockers, and coordinate between specialists (game design, C# dev, quest writing, world building, devops). Be decisive, concise, and always reference the shard design doc when planning. Speak with authority but keep answers actionable.`,
  },
  {
    id: "designer",
    name: "Game Designer",
    short: "Design",
    icon: "⚔️",
    accent: "#e8c46a",
    glow: "rgba(232,196,106,0.3)",
    border: "rgba(232,196,106,0.35)",
    greeting: "Ready to forge the mechanics of your world. What system shall we design?",
    system: `You are an expert game designer specializing in Ultima Online private server design. You focus on game mechanics, skill balance, risk/reward systems, player progression, and the custom skill template/combination system. You have deep knowledge of UO gameplay systems and ServUO capabilities. Always give concrete, actionable design advice grounded in UO mechanics. Be creative but always consider player fun and balance.`,
  },
  {
    id: "dev",
    name: "C# Developer",
    short: "Dev",
    icon: "⚙️",
    accent: "#4a9eff",
    glow: "rgba(74,158,255,0.3)",
    border: "rgba(74,158,255,0.35)",
    greeting: "Scripts sharpened and ready. What shall we build in ServUO today?",
    system: `You are a senior C# developer specializing in ServUO/RunUO emulator scripting. You write clean, performant C# code for UO shards. You know the ServUO codebase architecture deeply: Mobile, Item, Timer, Region, EventSink, QuestSystem, and custom script patterns. Always provide working C# code examples with comments. Focus on the TemplateManager system, auto-event timers, and ServUO scripting best practices. Format code in markdown code blocks.`,
  },
  {
    id: "quest",
    name: "Quest Writer",
    short: "Quest",
    icon: "📜",
    accent: "#b07dd4",
    glow: "rgba(176,125,212,0.3)",
    border: "rgba(176,125,212,0.35)",
    greeting: "The quill is ready and the lore awaits. What tale shall we weave?",
    system: `You are a creative writer and narrative designer specializing in Ultima Online lore and quest design. You craft compelling quest chains, NPC dialogue, world lore, and story arcs. You know Ultima lore deeply (Virtues, The Avatar, Britannia, The Guardian) and create stories that feel native to that world. Focus on multi-part quest chains, meaningful choices, and connecting quests to the shard's world events and skill template system. Write with an epic fantasy tone.`,
  },
  {
    id: "world",
    name: "World Architect",
    short: "World",
    icon: "🗺️",
    accent: "#3dba6f",
    glow: "rgba(61,186,111,0.3)",
    border: "rgba(61,186,111,0.35)",
    greeting: "The maps are unrolled. Where shall we carve the lands?",
    system: `You are a world-building expert specializing in Ultima Online shard design. You design zone layouts, dungeon structures, spawn placement, region configurations, and shard geography. You understand UO's facet system, map tools, and how to design spaces that support PvM and PvP. Focus on the three-tier risk zone system, dungeon design, and creating spaces that feel alive with the automated event system. Think like a dungeon master building a living world.`,
  },
  {
    id: "devops",
    name: "DevOps",
    short: "Ops",
    icon: "🛡️",
    accent: "#e05c5c",
    glow: "rgba(224,92,92,0.3)",
    border: "rgba(224,92,92,0.35)",
    greeting: "Fortifications ready. What infrastructure needs securing?",
    system: `You are a DevOps engineer specializing in game server hosting, specifically Ultima Online emulators on Linux. You handle VPS setup, ServUO deployment, networking (ports, firewalls, DDoS protection), automated backups, monitoring, and performance optimization. Give specific, actionable Linux commands and configurations. Be direct and security-conscious. Always consider the public-facing nature of the server.`,
  },
];

const styles = `
  @import url('https://fonts.googleapis.com/css2?family=Cinzel:wght@400;600;700&family=Crimson+Pro:ital,wght@0,300;0,400;0,500;1,300;1,400&family=JetBrains+Mono:wght@400;500&display=swap');

  * { box-sizing: border-box; margin: 0; padding: 0; }

  :root {
    --bg: #080705;
    --surface: #0f0d0a;
    --panel: #141109;
    --panel2: #1a1610;
    --border: rgba(180,140,60,0.18);
    --gold: #c9952a;
    --gold-dim: rgba(201,149,42,0.6);
    --text: #e4d8be;
    --text-dim: #8a7d65;
    --text-muted: #5a5040;
    --scrollbar: #2a2218;
  }

  body { background: var(--bg); }

  .war-room {
    display: flex;
    height: 100vh;
    width: 100%;
    background: var(--bg);
    font-family: 'Crimson Pro', serif;
    color: var(--text);
    overflow: hidden;
    position: relative;
  }

  /* Subtle noise overlay */
  .war-room::before {
    content: '';
    position: fixed;
    inset: 0;
    background-image: url("data:image/svg+xml,%3Csvg viewBox='0 0 256 256' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='noise'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.9' numOctaves='4' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23noise)' opacity='0.04'/%3E%3C/svg%3E");
    pointer-events: none;
    z-index: 0;
    opacity: 0.4;
  }

  /* ── SIDEBAR ── */
  .sidebar {
    width: 72px;
    background: var(--surface);
    border-right: 1px solid var(--border);
    display: flex;
    flex-direction: column;
    align-items: center;
    padding: 16px 0;
    gap: 6px;
    z-index: 10;
    flex-shrink: 0;
  }

  .sidebar-logo {
    font-family: 'Cinzel', serif;
    font-size: 18px;
    color: var(--gold);
    margin-bottom: 10px;
    letter-spacing: 2px;
    opacity: 0.9;
    text-align: center;
    line-height: 1.1;
    padding: 0 8px;
  }

  .sidebar-divider {
    width: 40px;
    height: 1px;
    background: var(--border);
    margin: 4px 0 8px;
  }

  .persona-btn {
    width: 52px;
    height: 52px;
    border-radius: 8px;
    border: 1px solid transparent;
    background: transparent;
    cursor: pointer;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 2px;
    transition: all 0.2s ease;
    position: relative;
  }

  .persona-btn:hover {
    background: var(--panel2);
    border-color: var(--border);
  }

  .persona-btn.active {
    border-color: var(--active-border, var(--gold));
    background: var(--panel2);
    box-shadow: 0 0 12px var(--active-glow, rgba(201,149,42,0.2)), inset 0 0 8px rgba(0,0,0,0.3);
  }

  .persona-btn.active::before {
    content: '';
    position: absolute;
    left: -1px;
    top: 20%;
    bottom: 20%;
    width: 2px;
    background: var(--active-accent, var(--gold));
    border-radius: 0 2px 2px 0;
  }

  .persona-icon { font-size: 20px; line-height: 1; }
  .persona-label {
    font-family: 'Cinzel', serif;
    font-size: 7.5px;
    letter-spacing: 0.5px;
    color: var(--text-dim);
    text-transform: uppercase;
  }
  .persona-btn.active .persona-label { color: var(--active-accent, var(--gold)); }

  .doc-btn {
    margin-top: auto;
    width: 52px;
    height: 52px;
    border-radius: 8px;
    border: 1px solid var(--border);
    background: transparent;
    cursor: pointer;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 2px;
    color: var(--text-dim);
    transition: all 0.2s;
    font-size: 18px;
  }

  .doc-btn:hover { background: var(--panel2); color: var(--gold); }
  .doc-btn.open { background: var(--panel2); color: var(--gold); border-color: var(--gold-dim); }
  .doc-btn-label {
    font-family: 'Cinzel', serif;
    font-size: 7px;
    letter-spacing: 0.5px;
    text-transform: uppercase;
  }

  /* ── MAIN AREA ── */
  .main {
    flex: 1;
    display: flex;
    flex-direction: column;
    overflow: hidden;
    z-index: 1;
  }

  /* ── HEADER ── */
  .header {
    padding: 14px 24px;
    border-bottom: 1px solid var(--border);
    display: flex;
    align-items: center;
    gap: 14px;
    background: var(--surface);
    flex-shrink: 0;
  }

  .header-icon { font-size: 24px; }

  .header-info { flex: 1; }

  .header-title {
    font-family: 'Cinzel', serif;
    font-size: 15px;
    font-weight: 600;
    color: var(--active-color, var(--gold));
    letter-spacing: 1px;
  }

  .header-sub {
    font-size: 12px;
    color: var(--text-dim);
    font-style: italic;
    margin-top: 1px;
  }

  .header-badge {
    display: flex;
    align-items: center;
    gap: 6px;
    background: var(--panel2);
    border: 1px solid var(--border);
    border-radius: 20px;
    padding: 4px 12px;
    font-family: 'Cinzel', serif;
    font-size: 9px;
    letter-spacing: 1px;
    color: var(--gold-dim);
    text-transform: uppercase;
  }

  .badge-dot {
    width: 6px;
    height: 6px;
    border-radius: 50%;
    background: var(--gold);
    animation: pulse 2s infinite;
  }

  @keyframes pulse {
    0%, 100% { opacity: 1; }
    50% { opacity: 0.4; }
  }

  /* ── CHAT AREA ── */
  .chat-area {
    flex: 1;
    overflow-y: auto;
    padding: 24px;
    display: flex;
    flex-direction: column;
    gap: 16px;
    scrollbar-width: thin;
    scrollbar-color: var(--scrollbar) transparent;
  }

  .chat-area::-webkit-scrollbar { width: 4px; }
  .chat-area::-webkit-scrollbar-thumb { background: var(--scrollbar); border-radius: 2px; }

  .message {
    display: flex;
    gap: 12px;
    max-width: 820px;
    animation: fadeUp 0.25s ease;
  }

  @keyframes fadeUp {
    from { opacity: 0; transform: translateY(6px); }
    to { opacity: 1; transform: translateY(0); }
  }

  .message.user { align-self: flex-end; flex-direction: row-reverse; }

  .message-avatar {
    width: 32px;
    height: 32px;
    border-radius: 6px;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 16px;
    flex-shrink: 0;
    border: 1px solid var(--border);
    background: var(--panel2);
  }

  .message.user .message-avatar {
    background: var(--panel2);
    border-color: rgba(201,149,42,0.3);
    font-size: 13px;
    font-family: 'Cinzel', serif;
    color: var(--gold-dim);
  }

  .message-bubble {
    padding: 12px 16px;
    border-radius: 8px;
    font-size: 15px;
    line-height: 1.65;
    max-width: 100%;
  }

  .message.assistant .message-bubble {
    background: var(--panel);
    border: 1px solid var(--border);
    color: var(--text);
    border-left: 2px solid var(--active-color, var(--gold));
  }

  .message.user .message-bubble {
    background: var(--panel2);
    border: 1px solid rgba(201,149,42,0.25);
    color: var(--text);
    text-align: right;
  }

  /* Code blocks in messages */
  .message-bubble pre {
    background: #0a0907;
    border: 1px solid var(--border);
    border-radius: 6px;
    padding: 12px;
    margin-top: 10px;
    overflow-x: auto;
    font-family: 'JetBrains Mono', monospace;
    font-size: 12px;
    color: #c8b87e;
    line-height: 1.5;
  }

  .message-bubble code {
    font-family: 'JetBrains Mono', monospace;
    font-size: 12.5px;
    background: rgba(201,149,42,0.1);
    padding: 1px 5px;
    border-radius: 3px;
    color: #d4a843;
  }

  .message-bubble pre code {
    background: none;
    padding: 0;
    color: inherit;
  }

  /* Typing indicator */
  .typing {
    display: flex;
    align-items: center;
    gap: 12px;
    animation: fadeUp 0.2s ease;
  }

  .typing-dots {
    display: flex;
    gap: 4px;
    padding: 12px 16px;
    background: var(--panel);
    border: 1px solid var(--border);
    border-left: 2px solid var(--active-color, var(--gold));
    border-radius: 8px;
  }

  .typing-dot {
    width: 6px;
    height: 6px;
    border-radius: 50%;
    background: var(--active-color, var(--gold));
    opacity: 0.4;
    animation: typingBounce 1.2s infinite;
  }
  .typing-dot:nth-child(2) { animation-delay: 0.2s; }
  .typing-dot:nth-child(3) { animation-delay: 0.4s; }

  @keyframes typingBounce {
    0%, 60%, 100% { transform: translateY(0); opacity: 0.4; }
    30% { transform: translateY(-4px); opacity: 1; }
  }

  /* Empty state */
  .empty-state {
    flex: 1;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 12px;
    color: var(--text-dim);
    text-align: center;
  }

  .empty-icon { font-size: 42px; opacity: 0.4; }

  .empty-greeting {
    font-family: 'Cinzel', serif;
    font-size: 13px;
    letter-spacing: 1px;
    color: var(--active-color, var(--gold));
    opacity: 0.8;
    max-width: 360px;
    line-height: 1.6;
    font-style: italic;
  }

  .empty-hint {
    font-size: 12px;
    color: var(--text-muted);
    margin-top: 4px;
  }

  .suggestion-chips {
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
    justify-content: center;
    margin-top: 8px;
    max-width: 500px;
  }

  .chip {
    padding: 6px 14px;
    border-radius: 20px;
    border: 1px solid var(--border);
    background: var(--panel);
    font-size: 12px;
    color: var(--text-dim);
    cursor: pointer;
    transition: all 0.2s;
    font-family: 'Crimson Pro', serif;
  }

  .chip:hover {
    border-color: var(--active-color, var(--gold));
    color: var(--active-color, var(--gold));
    background: var(--panel2);
  }

  /* ── INPUT BAR ── */
  .input-bar {
    padding: 16px 24px;
    border-top: 1px solid var(--border);
    background: var(--surface);
    display: flex;
    gap: 12px;
    align-items: flex-end;
    flex-shrink: 0;
  }

  .input-wrap {
    flex: 1;
    background: var(--panel);
    border: 1px solid var(--border);
    border-radius: 8px;
    display: flex;
    align-items: flex-end;
    gap: 0;
    transition: border-color 0.2s;
    overflow: hidden;
  }

  .input-wrap:focus-within {
    border-color: var(--active-color, var(--gold));
  }

  .input-field {
    flex: 1;
    background: transparent;
    border: none;
    outline: none;
    padding: 11px 14px;
    color: var(--text);
    font-family: 'Crimson Pro', serif;
    font-size: 15px;
    resize: none;
    min-height: 44px;
    max-height: 120px;
    line-height: 1.5;
  }

  .input-field::placeholder { color: var(--text-muted); }

  .send-btn {
    width: 44px;
    height: 44px;
    margin: 0;
    border: none;
    background: var(--active-color, var(--gold));
    cursor: pointer;
    display: flex;
    align-items: center;
    justify-content: center;
    transition: all 0.2s;
    flex-shrink: 0;
  }

  .send-btn:hover:not(:disabled) { filter: brightness(1.15); }
  .send-btn:disabled { opacity: 0.4; cursor: not-allowed; }

  .send-btn svg { width: 16px; height: 16px; fill: #0a0907; }

  /* ── DOC PANEL ── */
  .doc-overlay {
    position: fixed;
    inset: 0;
    background: rgba(0,0,0,0.5);
    z-index: 50;
    display: flex;
    align-items: stretch;
    justify-content: flex-end;
  }

  .doc-panel {
    width: 420px;
    max-width: 90vw;
    background: var(--surface);
    border-left: 1px solid var(--border);
    display: flex;
    flex-direction: column;
    animation: slideIn 0.25s ease;
  }

  @keyframes slideIn {
    from { transform: translateX(30px); opacity: 0; }
    to { transform: translateX(0); opacity: 1; }
  }

  .doc-header {
    padding: 18px 20px;
    border-bottom: 1px solid var(--border);
    display: flex;
    align-items: center;
    justify-content: space-between;
  }

  .doc-title {
    font-family: 'Cinzel', serif;
    font-size: 13px;
    letter-spacing: 1.5px;
    color: var(--gold);
    text-transform: uppercase;
  }

  .close-btn {
    width: 28px;
    height: 28px;
    border-radius: 4px;
    border: 1px solid var(--border);
    background: transparent;
    cursor: pointer;
    color: var(--text-dim);
    font-size: 16px;
    display: flex;
    align-items: center;
    justify-content: center;
    transition: all 0.2s;
  }

  .close-btn:hover { background: var(--panel2); color: var(--text); }

  .doc-content {
    flex: 1;
    overflow-y: auto;
    padding: 20px;
    font-size: 13px;
    line-height: 1.7;
    color: var(--text-dim);
    white-space: pre-wrap;
    font-family: 'JetBrains Mono', monospace;
    scrollbar-width: thin;
    scrollbar-color: var(--scrollbar) transparent;
  }

  /* Ornamental divider */
  .ornament {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 0 24px;
    color: var(--text-muted);
    font-size: 10px;
    letter-spacing: 3px;
    text-transform: uppercase;
    font-family: 'Cinzel', serif;
    flex-shrink: 0;
  }

  .ornament::before, .ornament::after {
    content: '';
    flex: 1;
    height: 1px;
    background: var(--border);
  }
`;

function formatMessage(text) {
  const parts = text.split(/(```[\s\S]*?```)/g);
  return parts.map((part, i) => {
    if (part.startsWith("```")) {
      const code = part.replace(/^```\w*\n?/, "").replace(/```$/, "");
      return <pre key={i}><code>{code}</code></pre>;
    }
    const inlined = part.split(/(`[^`]+`)/g).map((seg, j) => {
      if (seg.startsWith("`") && seg.endsWith("`")) {
        return <code key={j}>{seg.slice(1, -1)}</code>;
      }
      return seg;
    });
    return <span key={i}>{inlined}</span>;
  });
}

const SUGGESTIONS = {
  lead:     ["What's our first milestone?", "Break down the skill template feature", "What should we tackle this week?"],
  designer: ["Design the Spellblade template in detail", "How should Tier 3 risk/reward work?", "Balance PvP for the contested zones"],
  dev:      ["Write the TemplateManager class", "Code a Champion Spawn auto-reset timer", "How do I add a custom ability in ServUO?"],
  quest:    ["Write the opening quest for new players", "Create a Tier 3 lore hook", "Design a multi-part story arc"],
  world:    ["Design the layout for Tier 2 zones", "Plan a Tier 3 dungeon", "Where should Champion Spawns go?"],
  devops:   ["How do I set up ServUO on Ubuntu VPS?", "What ports need to be open?", "Set up automated backups"],
};

export default function WarRoom() {
  const [activeId, setActiveId] = useState("lead");
  const [conversations, setConversations] = useState(
    Object.fromEntries(PERSONAS.map((p) => [p.id, []]))
  );
  const [input, setInput] = useState("");
  const [loading, setLoading] = useState(false);
  const [showDoc, setShowDoc] = useState(false);
  const messagesEndRef = useRef(null);
  const textareaRef = useRef(null);

  const persona = PERSONAS.find((p) => p.id === activeId);
  const messages = conversations[activeId];

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages, loading]);

  const sendMessage = async (text) => {
    const msg = (text || input).trim();
    if (!msg || loading) return;
    setInput("");
    setLoading(true);

    const newMsgs = [...messages, { role: "user", content: msg }];
    setConversations((p) => ({ ...p, [activeId]: newMsgs }));

    try {
      const res = await fetch("https://api.anthropic.com/v1/messages", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          model: "claude-sonnet-4-20250514",
          max_tokens: 1000,
          system: `${persona.system}\n\n## Shard Design Document (shared reference)\n\n${SHARD_DOC}`,
          messages: newMsgs,
        }),
      });
      const data = await res.json();
      const reply = data.content?.[0]?.text || "No response.";
      setConversations((p) => ({
        ...p,
        [activeId]: [...newMsgs, { role: "assistant", content: reply }],
      }));
    } catch {
      setConversations((p) => ({
        ...p,
        [activeId]: [...newMsgs, { role: "assistant", content: "⚠️ Connection failed. Check your network and try again." }],
      }));
    }
    setLoading(false);
  };

  const handleKey = (e) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      sendMessage();
    }
  };

  const handleInput = (e) => {
    setInput(e.target.value);
    const el = textareaRef.current;
    if (el) { el.style.height = "auto"; el.style.height = Math.min(el.scrollHeight, 120) + "px"; }
  };

  return (
    <>
      <style>{styles}</style>
      <div className="war-room">

        {/* ── SIDEBAR ── */}
        <div className="sidebar">
          <div className="sidebar-logo">☩<br/>UO</div>
          <div className="sidebar-divider" />
          {PERSONAS.map((p) => (
            <button
              key={p.id}
              className={`persona-btn ${activeId === p.id ? "active" : ""}`}
              style={{
                "--active-border": p.border,
                "--active-glow": p.glow,
                "--active-accent": p.accent,
              }}
              onClick={() => setActiveId(p.id)}
              title={p.name}
            >
              <span className="persona-icon">{p.icon}</span>
              <span className="persona-label">{p.short}</span>
            </button>
          ))}
          <button
            className={`doc-btn ${showDoc ? "open" : ""}`}
            onClick={() => setShowDoc((v) => !v)}
            title="Shard Design Document"
          >
            📋
            <span className="doc-btn-label">Doc</span>
          </button>
        </div>

        {/* ── MAIN ── */}
        <div className="main" style={{ "--active-color": persona.accent }}>

          {/* Header */}
          <div className="header">
            <div className="header-icon">{persona.icon}</div>
            <div className="header-info">
              <div className="header-title">{persona.name}</div>
              <div className="header-sub">UO Shard War Room · ServUO / ClassicUO</div>
            </div>
            <div className="header-badge">
              <div className="badge-dot" style={{ background: persona.accent }} />
              Active
            </div>
          </div>

          {/* Chat */}
          <div className="chat-area">
            {messages.length === 0 ? (
              <div className="empty-state">
                <div className="empty-icon">{persona.icon}</div>
                <div className="empty-greeting">"{persona.greeting}"</div>
                <div className="empty-hint">— {persona.name}</div>
                <div className="suggestion-chips">
                  {SUGGESTIONS[activeId]?.map((s) => (
                    <button key={s} className="chip" onClick={() => sendMessage(s)}>{s}</button>
                  ))}
                </div>
              </div>
            ) : (
              messages.map((m, i) => (
                <div key={i} className={`message ${m.role}`}>
                  <div className="message-avatar" style={m.role === "assistant" ? { borderColor: persona.border } : {}}>
                    {m.role === "assistant" ? persona.icon : "⚜"}
                  </div>
                  <div className="message-bubble" style={m.role === "assistant" ? { "--active-color": persona.accent } : {}}>
                    {formatMessage(m.content)}
                  </div>
                </div>
              ))
            )}

            {loading && (
              <div className="typing">
                <div className="message-avatar" style={{ borderColor: persona.border }}>{persona.icon}</div>
                <div className="typing-dots" style={{ "--active-color": persona.accent }}>
                  <div className="typing-dot" style={{ background: persona.accent }} />
                  <div className="typing-dot" style={{ background: persona.accent }} />
                  <div className="typing-dot" style={{ background: persona.accent }} />
                </div>
              </div>
            )}
            <div ref={messagesEndRef} />
          </div>

          {/* Input */}
          <div className="input-bar">
            <div className="input-wrap">
              <textarea
                ref={textareaRef}
                className="input-field"
                placeholder={`Ask the ${persona.name}...`}
                value={input}
                onChange={handleInput}
                onKeyDown={handleKey}
                rows={1}
              />
              <button className="send-btn" onClick={() => sendMessage()} disabled={!input.trim() || loading}
                style={{ background: persona.accent }}>
                <svg viewBox="0 0 24 24"><path d="M2 21l21-9L2 3v7l15 2-15 2z"/></svg>
              </button>
            </div>
          </div>
        </div>

        {/* ── DOC OVERLAY ── */}
        {showDoc && (
          <div className="doc-overlay" onClick={(e) => e.target === e.currentTarget && setShowDoc(false)}>
            <div className="doc-panel">
              <div className="doc-header">
                <div className="doc-title">⚜ Shard Design Document</div>
                <button className="close-btn" onClick={() => setShowDoc(false)}>✕</button>
              </div>
              <div className="doc-content">{SHARD_DOC}</div>
            </div>
          </div>
        )}
      </div>
    </>
  );
}
