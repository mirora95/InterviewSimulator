const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../InterviewSimulator.Api/wwwroot/interview.js'), 'utf8');

// Minimal DOM boundary: parsing a received message as HTML must never be needed.
class Element {
    constructor(tagName) {
        this.tagName = tagName;
        this.children = [];
        this.value = '';
    }

    set innerHTML(value) {
        assert.equal(value, '', 'Received messages must not be parsed as HTML');
        this.children = [];
    }

    set textContent(value) { this.children = [String(value)]; }
    get textContent() { return this.children.map(child => typeof child === 'string' ? child : child.textContent).join(''); }
    appendChild(child) { this.children.push(child); }
}

async function createClient() {
    const elements = Object.fromEntries(['start', 'send', 'chat', 'answer', 'role', 'level'].map(id => [id, new Element('div')]));
    elements.role.value = 'C# Developer';
    elements.level.value = 'Junior';
    const handlers = {};
    const calls = [];
    const connection = {
        state: 'Connected',
        on: (name, handler) => { handlers[name] = handler; },
        start: async () => {},
        invoke: async (...args) => { calls.push(args); }
    };
    const builder = {
        withUrl() { return this; },
        withAutomaticReconnect() { return this; },
        build() { return connection; }
    };
    vm.runInNewContext(source, {
        document: {
            getElementById: id => elements[id],
            createElement: tag => new Element(tag),
            createTextNode: text => String(text)
        },
        signalR: {
            HubConnectionBuilder: function () { return builder; },
            HubConnectionState: { Connected: 'Connected' }
        },
        console: { log() {}, error() {} },
        alert: message => assert.fail(message)
    });
    // Let the completed SignalR startup enable controls before interactions.
    await Promise.resolve();
    return { elements, handlers, calls, connection };
}

for (const type of ['question', 'evaluation', 'error', '<svg onload=alert(3)>']) {
    test(`renders ${type} fields literally and preserves the sender label`, async () => {
        const { elements, handlers, calls } = await createClient();
        const message = {
            from: '<img src=x onerror=alert(1)>',
            type,
            text: '<b>Interface</b> & <img src=x onerror=alert(2)>'
        };

        handlers.ReceiveMessage(message);

        const row = elements.chat.children[0];
        assert.equal(row.textContent, `${message.from} [${type}]: ${message.text}`);
        assert.equal(row.children[0].tagName, 'b');
        assert.equal(row.children[0].textContent, message.from);
        assert.equal(row.children.filter(child => typeof child !== 'string').length, 1);

        if (type === 'question') {
            elements.answer.value = '  A contract.  ';

            await elements.send.onclick();

            assert.deepEqual(calls[0], ['SubmitAnswer', message.text, 'A contract.']);
            assert.equal(elements.answer.value, '');
        }
    });
}

test('preserves answer edits made while submission is pending', async () => {
    const { elements, handlers, calls, connection } = await createClient();
    let finishSubmission;
    const pending = new Promise(resolve => { finishSubmission = resolve; });
    connection.invoke = async (...args) => { calls.push(args); await pending; };
    handlers.ReceiveMessage({ from: 'interviewer', type: 'question', text: 'What is an interface?' });
    elements.answer.value = '  A contract.  ';

    const submission = elements.send.onclick();
    assert.deepEqual(calls[0], ['SubmitAnswer', 'What is an interface?', 'A contract.']);
    elements.answer.value = 'A contract that defines members.';
    finishSubmission();
    await submission;

    assert.equal(elements.answer.value, 'A contract that defines members.');
});

test('keeps the answer and allows retry when submission fails', async () => {
    const { elements, handlers, calls, connection } = await createClient();
    connection.invoke = async (...args) => {
        calls.push(args);
        if (calls.length === 1) throw new Error('Connection lost');
    };
    handlers.ReceiveMessage({ from: 'interviewer', type: 'question', text: 'What is an interface?' });
    elements.answer.value = 'A contract.';

    await elements.send.onclick();

    assert.equal(elements.answer.value, 'A contract.');
    assert.equal(elements.send.disabled, false);

    await elements.send.onclick();

    assert.equal(calls.length, 2);
    assert.equal(elements.answer.value, '');
    assert.equal(elements.send.disabled, false);
});

test('ignores repeated answer submissions while evaluation is pending', async () => {
    const { elements, handlers, calls, connection } = await createClient();
    let finishSubmission;
    const pending = new Promise(resolve => { finishSubmission = resolve; });
    connection.invoke = async (...args) => { calls.push(args); await pending; };
    handlers.ReceiveMessage({ from: 'interviewer', type: 'question', text: 'What is an interface?' });
    elements.answer.value = 'A contract.';

    const firstSubmission = elements.send.onclick();
    const disabledWhilePending = elements.send.disabled;
    const secondSubmission = elements.send.onclick();
    const pendingCallCount = calls.length;
    finishSubmission();
    await Promise.all([firstSubmission, secondSubmission]);

    assert.equal(pendingCallCount, 1);
    assert.equal(disabledWhilePending, true);
    assert.equal(elements.send.disabled, false);
    assert.deepEqual(calls[0], ['SubmitAnswer', 'What is an interface?', 'A contract.']);
});

test('keeps the current interview when Start is attempted during evaluation', async () => {
    const { elements, handlers, calls, connection } = await createClient();
    let finishSubmission;
    const pending = new Promise(resolve => { finishSubmission = resolve; });
    connection.invoke = async (...args) => { calls.push(args); await pending; };
    handlers.ReceiveMessage({ from: 'interviewer', type: 'question', text: 'What is an interface?' });
    elements.answer.value = 'A contract.';
    const transcript = elements.chat.textContent;

    const submission = elements.send.onclick();
    const startDisabledWhilePending = elements.start.disabled;
    const restart = elements.start.onclick();
    const pendingCallCount = calls.length;
    const pendingTranscript = elements.chat.textContent;
    finishSubmission();
    await Promise.all([submission, restart]);

    assert.equal(pendingCallCount, 1);
    assert.equal(startDisabledWhilePending, true);
    assert.equal(pendingTranscript, transcript);
    assert.equal(elements.start.disabled, false);
    assert.equal(elements.send.disabled, false);
});

test('waits for interview startup before allowing another Start or Submit', async () => {
    const { elements, handlers, calls, connection } = await createClient();
    let finishStartup;
    const pending = new Promise(resolve => { finishStartup = resolve; });
    connection.invoke = async (...args) => { calls.push(args); await pending; };

    const startup = elements.start.onclick();
    const startDisabledWhilePending = elements.start.disabled;
    const sendDisabledWhilePending = elements.send.disabled;
    const secondStartup = elements.start.onclick();
    handlers.ReceiveMessage({ from: 'interviewer', type: 'question', text: 'What is an interface?' });
    elements.answer.value = 'A contract.';
    const submission = elements.send.onclick();
    const pendingCallCount = calls.length;
    finishStartup();
    await Promise.all([startup, secondStartup, submission]);

    assert.equal(pendingCallCount, 1);
    assert.equal(startDisabledWhilePending, true);
    assert.equal(sendDisabledWhilePending, true);
    assert.deepEqual(calls[0], ['StartInterview', 'C# Developer', 'Junior']);
    assert.equal(elements.answer.value, 'A contract.');
    assert.equal(elements.start.disabled, false);
    assert.equal(elements.send.disabled, false);

    await elements.send.onclick();
    assert.deepEqual(calls[1], ['SubmitAnswer', 'What is an interface?', 'A contract.']);
});

test('allows retry after interview startup fails', async () => {
    const { elements, calls, connection } = await createClient();
    connection.invoke = async (...args) => {
        calls.push(args);
        if (calls.length === 1) throw new Error('Connection lost');
    };

    await elements.start.onclick();
    assert.equal(elements.start.disabled, false);
    assert.equal(elements.send.disabled, false);

    await elements.start.onclick();
    assert.equal(calls.length, 2);
    assert.deepEqual(calls[1], ['StartInterview', 'C# Developer', 'Junior']);
    assert.equal(elements.start.disabled, false);
    assert.equal(elements.send.disabled, false);
});
