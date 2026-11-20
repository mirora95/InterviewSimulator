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
        this.attributes = {};
    }

    set innerHTML(value) {
        assert.equal(value, '', 'Received messages must not be parsed as HTML');
        this.children = [];
    }

    set textContent(value) { this.children = [String(value)]; }
    get textContent() { return this.children.map(child => typeof child === 'string' ? child : child.textContent).join(''); }
    appendChild(child) { this.children.push(child); }
    setAttribute(name, value) { this.attributes[name] = String(value); }
}

for (const type of ['question', 'evaluation', 'error', '<svg onload=alert(3)>']) {
    test(`renders ${type} fields literally and preserves the sender label`, async () => {
        const elements = Object.fromEntries(['start', 'send', 'chat', 'answer'].map(id => [id, new Element('div')]));
        const handlers = {};
        const calls = [];
        const connection = {
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
            signalR: { HubConnectionBuilder: function () { return builder; } },
            console: { log() {}, error() {} },
            alert: message => assert.fail(message)
        });
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
            elements.answer.value = 'A contract.';

            await elements.send.onclick();

            assert.deepEqual(calls[0], ['SubmitAnswer', message.text, 'A contract.']);
            assert.equal(elements.answer.value, '');
        }
    });
}

for (const stage of ['evaluation', 'follow-up']) {
    test(`shows a safe recoverable error when ${stage} fails after answer submission`, async () => {
        const elements = Object.fromEntries(['start', 'send', 'chat', 'answer', 'role', 'level'].map(id => [id, new Element('div')]));
        const handlers = {};
        const calls = [];
        let fail = true;
        const question = 'What is an interface?';
        const receive = (type, text) => handlers.ReceiveMessage({ from: 'interviewer', type, text });
        const connection = {
            state: 'Connected',
            on: (name, handler) => { handlers[name] = handler; },
            start: async () => {},
            invoke: async (...args) => {
                calls.push(args);
                if (args[0] === 'StartInterview') return receive('question', question);
                receive('info', 'Evaluating...');
                if (stage === 'follow-up' || !fail) receive('evaluation', 'Useful feedback.');
                if (fail) throw new Error('<secret-provider-detail>');
            }
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
        await Promise.resolve();
        await elements.start.onclick();
        elements.answer.value = '  A contract.  ';

        await elements.send.onclick();

        const errors = elements.chat.children.filter(row => row.attributes.role === 'alert');
        assert.equal(errors.length, 1, 'The learner must see the failed operation');
        assert.equal(errors[0].textContent, "system [error]: We couldn't finish processing your answer. Your text is kept. Check any feedback above before trying again.");
        assert.ok(!elements.chat.textContent.includes('<secret-provider-detail>'));
        assert.equal(elements.answer.value, '  A contract.  ');
        assert.ok(elements.chat.textContent.includes(question));
        if (stage === 'follow-up') assert.ok(elements.chat.textContent.includes('Useful feedback.'));
        assert.equal(calls.length, 2, 'There is no automatic retry');

        fail = false;
        await elements.send.onclick();

        assert.equal(calls.length, 3);
        assert.deepEqual(calls[2], ['SubmitAnswer', question, 'A contract.']);
        assert.equal(elements.answer.value, '');
        assert.equal(elements.chat.children.filter(row => row.attributes.role === 'alert').length, 1);
    });
}
