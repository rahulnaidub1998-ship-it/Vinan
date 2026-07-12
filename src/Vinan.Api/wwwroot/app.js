const chatLog = document.querySelector("#chatLog");
const composer = document.querySelector("#composer");
const promptInput = document.querySelector("#promptInput");
const memoryList = document.querySelector("#memoryList");
const reminderList = document.querySelector("#reminderList");
const viewPanel = document.querySelector("#viewPanel");
const synth = window.speechSynthesis;
const API_BASE = window.location.protocol === "file:" ? "http://127.0.0.1:5017" : window.location.origin;
let apiOnline = false;
let voiceRepliesEnabled = false;

const state = {
  memories: JSON.parse(localStorage.getItem("vinan.memories") || "[]"),
  audit: JSON.parse(localStorage.getItem("vinan.audit") || "[]"),
  reminders: JSON.parse(localStorage.getItem("vinan.reminders") || "[]"),
  notes: JSON.parse(localStorage.getItem("vinan.notes") || "[]"),
  pendingActions: JSON.parse(localStorage.getItem("vinan.pendingActions") || "[]"),
  permissions: {
    Calendar: "Read",
    Gmail: "Prepare",
    Reminders: "Execute",
    Files: "Confirm",
    Finance: "Restricted",
    SmartHome: "Confirm",
    Production: "Restricted",
  },
};

const seedMessages = [
  {
    role: "vinan",
    text:
      "VINAN is online in local prototype mode. I can hold approved memory, create local reminders, capture notes, and pause risky actions for approval.",
  },
  {
    role: "vinan",
    text:
      "Try: Remember that I prefer concise answers. Create a reminder for tomorrow. Note my passport renewal is important. Or calculate 42 * 12.",
  },
];

function save() {
  localStorage.setItem("vinan.memories", JSON.stringify(state.memories));
  localStorage.setItem("vinan.audit", JSON.stringify(state.audit));
  localStorage.setItem("vinan.reminders", JSON.stringify(state.reminders));
  localStorage.setItem("vinan.notes", JSON.stringify(state.notes));
  localStorage.setItem("vinan.pendingActions", JSON.stringify(state.pendingActions));
}

function addAudit(action, level = "Level 1") {
  state.audit.unshift({
    action,
    level,
    time: new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }),
  });
  state.audit = state.audit.slice(0, 12);
  save();
  renderView(document.querySelector(".nav-item.active")?.dataset.view || "overview");
}

function speak(text) {
  if (!synth) return;
  synth.cancel();
  const utterance = new SpeechSynthesisUtterance(text);
  utterance.rate = 0.96;
  utterance.pitch = 0.92;
  synth.speak(utterance);
}

function addMessage(role, text) {
  const item = document.createElement("div");
  item.className = `message ${role}`;
  item.textContent = text;
  chatLog.appendChild(item);
  chatLog.scrollTop = chatLog.scrollHeight;
  if (role === "vinan" && voiceRepliesEnabled) speak(text);
}

function memoryText(memory) {
  return typeof memory === "string" ? memory : memory.text;
}

async function checkApi() {
  try {
    const response = await fetch(`${API_BASE}/api/health`);
    apiOnline = response.ok;
  } catch {
    apiOnline = false;
  }

  const status = document.querySelector("#apiStatus");
  if (status) status.textContent = apiOnline ? "API connected" : "Local prototype";
  if (apiOnline) await hydrateFromApi();
}

async function hydrateFromApi() {
  try {
    const [memoryResponse, reminderResponse, actionResponse, permissionResponse, auditResponse] = await Promise.all([
      fetch(`${API_BASE}/api/memory`),
      fetch(`${API_BASE}/api/reminders`),
      fetch(`${API_BASE}/api/actions`),
      fetch(`${API_BASE}/api/permissions`),
      fetch(`${API_BASE}/api/audit`),
    ]);

    if (![memoryResponse, reminderResponse, actionResponse, permissionResponse, auditResponse].every((response) => response.ok)) {
      return;
    }

    state.memories = await memoryResponse.json();
    state.reminders = await reminderResponse.json();
    const actions = await actionResponse.json();
    state.pendingActions = actions.map((action) => ({
      ...action,
      level: formatRiskLabel(action.riskLevel),
    }));
    const permissions = await permissionResponse.json();
    state.permissions = Object.fromEntries(permissions.map((permission) => [permission.name, permission.level]));
    const events = await auditResponse.json();
    state.audit = events.slice(0, 12).map((event) => ({
      action: event.action,
      level: formatRiskLabel(event.riskLevel),
      time: new Date(event.createdAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }),
    }));

    save();
    renderMemory();
    renderReminders();
    renderPendingAction();
    renderView(document.querySelector(".nav-item.active")?.dataset.view || "overview");
  } catch {
    apiOnline = false;
    document.querySelector("#apiStatus").textContent = "Local prototype";
  }
}

function renderMemory() {
  memoryList.innerHTML = "";
  if (!state.memories.length) {
    const empty = document.createElement("div");
    empty.className = "memory-item";
    empty.textContent = "No approved memories yet.";
    memoryList.appendChild(empty);
    return;
  }

  state.memories.forEach((memory, index) => {
    const row = document.createElement("div");
    row.className = "memory-item";
    const label = document.createElement("span");
    label.textContent = memoryText(memory);
    const remove = document.createElement("button");
    remove.type = "button";
    remove.textContent = "Forget";
    remove.addEventListener("click", async () => {
      if (apiOnline && memory.id) {
        const response = await fetch(`${API_BASE}/api/memory/${memory.id}`, { method: "DELETE" });
        if (!response.ok) return;
      }
      state.memories.splice(index, 1);
      if (apiOnline) await syncAuditFromApi();
      else addAudit("Memory deleted", "Level 2");
      renderMemory();
      save();
    });
    row.append(label, remove);
    memoryList.appendChild(row);
  });
}

function renderReminders() {
  reminderList.innerHTML = "";
  if (!state.reminders.length) {
    const empty = document.createElement("div");
    empty.className = "reminder-item";
    empty.innerHTML = '<span class="item-meta">No local reminders yet.</span>';
    reminderList.appendChild(empty);
    return;
  }

  state.reminders.forEach((reminder, index) => {
    const row = document.createElement("div");
    row.className = "reminder-item";
    row.innerHTML = `
      <header>
        <strong>${escapeHtml(reminder.title)}</strong>
        <button type="button">Done</button>
      </header>
      <span class="item-meta">${escapeHtml(reminder.when)}</span>
    `;
    row.querySelector("button").addEventListener("click", async () => {
      if (apiOnline && reminder.id) {
        const response = await fetch(`${API_BASE}/api/reminders/${reminder.id}/complete`, { method: "POST" });
        if (!response.ok) return;
      }
      state.reminders.splice(index, 1);
      if (apiOnline) await syncAuditFromApi();
      else addAudit("Reminder completed", "Level 2");
      renderReminders();
      save();
    });
    reminderList.appendChild(row);
  });
}

function renderPendingAction() {
  const action = state.pendingActions[0];
  const actionText = document.querySelector("#pendingAction");
  const risk = document.querySelector("#pendingRisk");
  if (!action) {
    actionText.textContent =
      "No pending external action. VINAN will pause here before communication, finance, production, or physical actions.";
    risk.textContent = "Idle";
    return;
  }
  actionText.textContent = action.summary;
  risk.textContent = action.level;
}

function addReminder(title, when = "Scheduled locally") {
  state.reminders.unshift({
    id: crypto.randomUUID?.() || String(Date.now()),
    title,
    when,
    createdAt: new Date().toISOString(),
  });
  state.reminders = state.reminders.slice(0, 12);
  renderReminders();
  addAudit("Reminder created", "Level 2");
  save();
}

function addNote(text) {
  state.notes.unshift({
    id: crypto.randomUUID?.() || String(Date.now()),
    text,
    createdAt: new Date().toISOString(),
  });
  state.notes = state.notes.slice(0, 20);
  addAudit("Note captured", "Level 2");
  save();
}

function queueAction(summary, level = "Level 3") {
  state.pendingActions.unshift({
    id: crypto.randomUUID?.() || String(Date.now()),
    summary,
    level,
    createdAt: new Date().toISOString(),
  });
  state.pendingActions = state.pendingActions.slice(0, 6);
  renderPendingAction();
  addAudit("Action queued for approval", level);
  save();
}

function responseFor(input) {
  const text = input.trim();
  const lower = text.toLowerCase();

  if (lower.includes("remember")) {
    const memory = text.replace(/^(vinan,?\s*)?remember\s*(that)?\s*/i, "").trim();
    if (memory) {
      state.memories.unshift({
        id: crypto.randomUUID?.() || String(Date.now()),
        text: memory,
        category: "Preference",
        createdAt: new Date().toISOString(),
      });
      state.memories = state.memories.filter(
        (item, index, items) => items.findIndex((candidate) => memoryText(candidate) === memoryText(item)) === index,
      ).slice(0, 10);
      renderMemory();
      addAudit("Approved memory stored", "Level 2");
      return `I saved this as approved memory: ${memory}`;
    }
  }

  if (lower.includes("what do you know about me")) {
    addAudit("Memory reviewed", "Level 1");
    if (!state.memories.length) return "I do not have approved long-term memories yet.";
    return `Here is what I currently remember: ${state.memories.map(memoryText).join("; ")}.`;
  }

  if (lower.includes("forget") || lower.includes("delete")) {
    return "Use the memory panel to delete a specific memory, or delete all stored prototype memory.";
  }

  if (lower.includes("reminder")) {
    const title = text.replace(/^(vinan,?\s*)?(create|add|set)?\s*a?\s*reminder\s*(to|for)?\s*/i, "").trim() || text;
    const when = lower.includes("tomorrow") ? "Tomorrow" : lower.includes("today") ? "Today" : "Scheduled locally";
    addReminder(title, when);
    return `I created a local reminder: ${title}.`;
  }

  if (lower.startsWith("note ") || lower.includes("take a note") || lower.includes("write this down")) {
    const note = text
      .replace(/^(vinan,?\s*)?(take a note|note|write this down)\s*:?\s*/i, "")
      .trim();
    if (note) {
      addNote(note);
      return `I captured this note: ${note}`;
    }
  }

  if (lower.includes("transfer") || lower.includes("buy ") || lower.includes("deploy") || lower.includes("unlock")) {
    queueAction(`High-risk request blocked pending strong confirmation: "${text}".`, "Level 4");
    return "That is a high-risk action. I queued it for strong confirmation and will not execute it automatically.";
  }

  if (lower.includes("calendar") || lower.includes("meeting") || lower.includes("send") || lower.includes("email")) {
    queueAction(`Prepare action from request: "${text}". Confirmation is required before execution.`, "Level 3");
    return "I prepared that as a pending action and paused for confirmation.";
  }

  if (lower.includes("time") || lower.includes("date")) {
    addAudit("Date and time checked", "Level 1");
    return `It is ${new Date().toLocaleString([], { dateStyle: "full", timeStyle: "short" })}.`;
  }

  if (lower.startsWith("calculate ") || lower.startsWith("calc ")) {
    const expression = text.replace(/^(calculate|calc)\s*/i, "");
    const result = calculate(expression);
    addAudit("Calculator used", "Level 1");
    return result === null ? "I can calculate basic arithmetic expressions only in this prototype." : `${expression} = ${result}`;
  }

  if (lower.includes("permission") || lower.includes("access")) {
    document.querySelector('[data-view="permissions"]').click();
    return "Here are the current permission levels.";
  }

  if (lower.includes("roadmap") || lower.includes("build")) {
    document.querySelector('[data-view="roadmap"]').click();
    return "I opened the build roadmap so we can keep the long vision connected to the next milestone.";
  }

  addAudit("Conversation response generated", "Level 1");
  return "I can help shape that into a plan, a memory, a reminder, a note, or an approval-based action. Sensitive work stays gated.";
}

async function responseForRemote(input) {
  if (!apiOnline) return null;

  try {
    const response = await fetch(`${API_BASE}/api/conversation/message`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ message: input }),
    });
    if (!response.ok) return null;

    const result = await response.json();
    if (result.memory?.text) {
      state.memories.unshift(result.memory);
      state.memories = state.memories.filter(
        (item, index, items) => items.findIndex((candidate) => memoryText(candidate) === memoryText(item)) === index,
      ).slice(0, 10);
      renderMemory();
    }
    if (result.reminder?.title) {
      state.reminders.unshift({
        id: result.reminder.id,
        title: result.reminder.title,
        when: result.reminder.when,
        createdAt: result.reminder.createdAt,
      });
      renderReminders();
    }
    if (result.pendingAction?.summary) {
      state.pendingActions.unshift({
        id: result.pendingAction.id,
        summary: result.pendingAction.summary,
        level: formatRiskLabel(result.pendingAction.riskLevel),
        createdAt: result.pendingAction.createdAt,
      });
      renderPendingAction();
    }
    await syncAuditFromApi();
    save();
    return result.reply;
  } catch {
    apiOnline = false;
    document.querySelector("#apiStatus").textContent = "Local prototype";
    return null;
  }
}

async function syncAuditFromApi() {
  try {
    const response = await fetch(`${API_BASE}/api/audit`);
    if (!response.ok) return;
    const events = await response.json();
    state.audit = events.slice(0, 12).map((event) => ({
      action: event.action,
      level: formatRiskLabel(event.riskLevel),
      time: new Date(event.createdAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }),
    }));
    save();
    renderView(document.querySelector(".nav-item.active")?.dataset.view || "overview");
  } catch {
    apiOnline = false;
  }
}

async function clearAllMemories() {
  if (apiOnline) {
    for (const memory of state.memories.filter((item) => item.id)) {
      const response = await fetch(`${API_BASE}/api/memory/${memory.id}`, { method: "DELETE" });
      if (!response.ok) {
        addMessage("vinan", "I could not delete every memory. I refreshed the list to keep it accurate.");
        await hydrateFromApi();
        return;
      }
    }
  }

  state.memories = [];
  renderMemory();
  if (apiOnline) await syncAuditFromApi();
  else addAudit("All prototype memory deleted", "Level 2");
  save();
}

async function clearAllReminders() {
  if (apiOnline) {
    for (const reminder of state.reminders.filter((item) => item.id)) {
      const response = await fetch(`${API_BASE}/api/reminders/${reminder.id}/complete`, { method: "POST" });
      if (!response.ok) {
        addMessage("vinan", "I could not clear every reminder. I refreshed the queue to keep it accurate.");
        await hydrateFromApi();
        return;
      }
    }
  }

  state.reminders = [];
  renderReminders();
  if (apiOnline) await syncAuditFromApi();
  else addAudit("All reminders cleared", "Level 2");
  save();
}

async function resolvePendingAction(decision) {
  const action = state.pendingActions[0];
  if (!action) {
    addMessage("vinan", `There is no pending action to ${decision}.`);
    return;
  }

  if (apiOnline && action.id) {
    const response = await fetch(`${API_BASE}/api/actions/${action.id}/${decision}`, { method: "POST" });
    if (!response.ok) {
      addMessage("vinan", "I could not record that decision. The action remains paused.");
      await hydrateFromApi();
      return;
    }
  }

  state.pendingActions.shift();
  renderPendingAction();
  if (apiOnline) await syncAuditFromApi();
  else addAudit(`Pending action ${decision === "approve" ? "approved" : "denied"}`, action.level);

  addMessage(
    "vinan",
    decision === "approve"
      ? "Approved and recorded. No external connector is enabled yet, so nothing was executed."
      : "Denied and recorded. The action was kept from executing.",
  );
  save();
}

function renderView(view) {
  const auditItems = state.audit
    .map((item) => `<div class="audit-item"><strong>${escapeHtml(item.action)}</strong><br><span>${escapeHtml(item.level)} · ${escapeHtml(item.time)}</span></div>`)
    .join("");

  const templates = {
    overview: `
      <div class="view-grid">
        <div class="metric-card"><strong>${state.memories.length}</strong><span>Approved memories</span></div>
        <div class="metric-card"><strong>${state.reminders.length}</strong><span>Local reminders</span></div>
        <div class="metric-card"><strong>${state.pendingActions.length}</strong><span>Pending approvals</span></div>
      </div>
    `,
    memory: `
      <section class="module">
        <div class="module-header"><div><p class="panel-label">Memory Design</p><h3>Personal Knowledge</h3></div></div>
        <div class="roadmap-grid">
          <div class="roadmap-item"><strong>Profile</strong><br><span>Name, role, languages, communication style.</span></div>
          <div class="roadmap-item"><strong>Preferences</strong><br><span>Response length, tools, routines, travel defaults.</span></div>
          <div class="roadmap-item"><strong>Projects</strong><br><span>Documents, decisions, technical context.</span></div>
          <div class="roadmap-item"><strong>Temporary</strong><br><span>Conversation-only context that can expire.</span></div>
        </div>
      </section>
    `,
    permissions: `
      <section class="module">
        <div class="module-header"><div><p class="panel-label">Permission Model</p><h3>Tool Access</h3></div></div>
        <div class="permission-grid">
          ${Object.entries(state.permissions)
            .map(([name, level]) => `<div class="permission-item"><div><strong>${name}</strong><br><span>${descriptionFor(level)}</span></div><span class="select-pill">${level}</span></div>`)
            .join("")}
        </div>
      </section>
    `,
    automation: `
      <section class="module">
        <div class="module-header"><div><p class="panel-label">Automation</p><h3>Monitors and Workflows</h3></div></div>
        <div class="roadmap-grid">
          <div class="roadmap-item"><strong>Daily Briefing</strong><br><span>Calendar, priority tasks, unread signals.</span></div>
          <div class="roadmap-item"><strong>Follow-up Watch</strong><br><span>Unanswered messages and deadlines.</span></div>
          <div class="roadmap-item"><strong>Approval Queue</strong><br><span>External actions wait for confirmation.</span></div>
          <div class="roadmap-item"><strong>Quiet Hours</strong><br><span>Notification rules by time and priority.</span></div>
        </div>
      </section>
      <section class="module">
        <div class="module-header"><div><p class="panel-label">Notes</p><h3>Scratch Memory</h3></div></div>
        <form class="quick-form" id="noteForm">
          <textarea id="noteInput" placeholder="Capture a local note for this prototype"></textarea>
          <button type="submit">Save Note</button>
        </form>
        <div class="note-list">${renderNotesHtml()}</div>
      </section>
    `,
    roadmap: `
      <section class="module">
        <div class="module-header"><div><p class="panel-label">First Year</p><h3>Build Sequence</h3></div></div>
        <div class="roadmap-grid">
          <div class="roadmap-item"><strong>Months 1-2</strong><br><span>Identity, auth, chat, voice, basic tools, audit.</span></div>
          <div class="roadmap-item"><strong>Months 3-4</strong><br><span>Memory, reminders, notes, calendar, email summaries.</span></div>
          <div class="roadmap-item"><strong>Months 5-6</strong><br><span>Files, document Q&A, notifications, permissions dashboard.</span></div>
          <div class="roadmap-item"><strong>Months 7-12</strong><br><span>Workflows, coding assistant, finance, travel, smart home.</span></div>
        </div>
      </section>
    `,
  };

  const auditBlock = `
    <section class="module">
      <div class="module-header"><div><p class="panel-label">Audit</p><h3>Recent Activity</h3></div></div>
      <div class="audit-list">${auditItems || '<div class="audit-item"><span>No activity yet.</span></div>'}</div>
    </section>
  `;

  viewPanel.innerHTML = `${templates[view] || templates.overview}${auditBlock}`;
  wireDynamicViewControls();
}

function renderNotesHtml() {
  if (!state.notes.length) return '<div class="note-item"><span class="item-meta">No local notes yet.</span></div>';
  return state.notes
    .map(
      (note) => `
        <div class="note-item">
          <header><strong>${new Date(note.createdAt).toLocaleDateString()}</strong><button type="button" data-note-id="${note.id}">Delete</button></header>
          <span>${escapeHtml(note.text)}</span>
        </div>
      `,
    )
    .join("");
}

function wireDynamicViewControls() {
  const noteForm = document.querySelector("#noteForm");
  if (noteForm) {
    noteForm.addEventListener("submit", (event) => {
      event.preventDefault();
      const input = document.querySelector("#noteInput");
      const value = input.value.trim();
      if (!value) return;
      addNote(value);
      input.value = "";
      renderView("automation");
    });
  }

  document.querySelectorAll("[data-note-id]").forEach((button) => {
    button.addEventListener("click", () => {
      state.notes = state.notes.filter((note) => note.id !== button.dataset.noteId);
      addAudit("Note deleted", "Level 2");
      renderView("automation");
    });
  });
}

function descriptionFor(level) {
  return {
    Read: "VINAN may inspect information.",
    Prepare: "VINAN may draft but not execute.",
    Confirm: "VINAN must ask before acting.",
    Execute: "VINAN may act inside configured limits.",
    Restricted: "VINAN must not execute automatically.",
  }[level];
}

function formatRiskLabel(level) {
  return String(level).replace(/^Level(\d)$/i, "Level $1");
}

function calculate(expression) {
  if (!/^[\d\s().+\-*/%]+$/.test(expression)) return null;
  try {
    const result = Function(`"use strict"; return (${expression});`)();
    return Number.isFinite(result) ? result : null;
  } catch {
    return null;
  }
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

document.querySelectorAll(".nav-item").forEach((button) => {
  button.addEventListener("click", () => {
    document.querySelectorAll(".nav-item").forEach((item) => item.classList.remove("active"));
    button.classList.add("active");
    renderView(button.dataset.view);
  });
});

composer.addEventListener("submit", (event) => {
  event.preventDefault();
  const value = promptInput.value.trim();
  if (!value) return;
  addMessage("user", value);
  promptInput.value = "";
  window.setTimeout(async () => {
    const remoteReply = await responseForRemote(value);
    addMessage("vinan", remoteReply || responseFor(value));
  }, 220);
});

document.querySelector("#clearChat").addEventListener("click", () => {
  chatLog.innerHTML = "";
  seedMessages.forEach((message) => addMessage(message.role, message.text));
  addAudit("Conversation cleared", "Level 1");
});

document.querySelector("#forgetAll").addEventListener("click", () => {
  void clearAllMemories();
});

document.querySelector("#clearReminders").addEventListener("click", () => {
  void clearAllReminders();
});

document.querySelector("#approveAction").addEventListener("click", () => void resolvePendingAction("approve"));

document.querySelector("#denyAction").addEventListener("click", () => void resolvePendingAction("deny"));

document.querySelector("#voiceButton").addEventListener("click", () => {
  const Recognition = window.SpeechRecognition || window.webkitSpeechRecognition;
  if (!Recognition) {
    addMessage("vinan", "Speech recognition is not available in this browser. Spoken replies are enabled when your browser allows them.");
    addAudit("Voice unavailable", "Level 1");
    return;
  }

  voiceRepliesEnabled = true;
  const recognition = new Recognition();
  recognition.lang = "en-US";
  recognition.interimResults = false;
  recognition.onresult = (event) => {
    const transcript = event.results[0][0].transcript;
    promptInput.value = transcript;
    composer.requestSubmit();
  };
  recognition.onerror = () => addMessage("vinan", "I could not hear that clearly. Please try again or type the command.");
  recognition.start();
  addAudit("Voice listening started", "Level 1");
});

document.querySelector("#exportButton").addEventListener("click", () => {
  const blob = new Blob(
    [
      JSON.stringify(
        {
          memories: state.memories,
          reminders: state.reminders,
          notes: state.notes,
          pendingActions: state.pendingActions,
          audit: state.audit,
        },
        null,
        2,
      ),
    ],
    { type: "application/json" },
  );
  const link = document.createElement("a");
  link.href = URL.createObjectURL(blob);
  link.download = "vinan-memory-export.json";
  link.click();
  URL.revokeObjectURL(link.href);
  addAudit("Memory export generated", "Level 2");
});

renderMemory();
renderReminders();
renderPendingAction();
renderView("overview");
seedMessages.forEach((message) => addMessage(message.role, message.text));
checkApi();
