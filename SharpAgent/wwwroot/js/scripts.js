var connection = null
var realUserName = null
var conversationId = crypto.randomUUID()
updateConnectionStatus(false)

document.getElementById('userName').addEventListener('keydown', function (event) {
    if (!realUserName && event.key === 'Enter') {
        submitName();
    }
});

document.getElementById('chatInput').addEventListener('keydown', function (event) {
    const textValue = document.getElementById('chatInput').value;
    if (textValue && event.key === 'Enter') {
        sendMessage();
    }
});

function submitName() {
    const userName = document.getElementById('userName').value;
    if (userName) {
        document.getElementById('namePrompt').classList.add('hidden');

        realUserName = userName;
        connectionWithName();
    } else {
        alert('Please enter your name');
    }
}

function connectionWithName() {
    document.getElementById('chatPage').classList.remove('hidden');

    connection = new signalR.HubConnectionBuilder().withUrl(`/chat`).withAutomaticReconnect().build();
    bindConnectionMessages(connection);
    connection.start().then(() => {
        updateConnectionStatus(true);
        onConnected(connection);
    }).catch(error => {
        updateConnectionStatus(false);
        console.error(error);
    })
}

function bindConnectionMessages(connection) {
    connection.on('newMessage', (name, message) => {
        appendMessage(false, `${name}: ${message}`);
    });
    connection.on('newMessageWithId', (name, id, message) => {
        appendMessageWithId(id, `${message}`);
    });
    connection.onclose(() => {
        updateConnectionStatus(false);
    });
}

function onConnected(connection) {
    console.log('connection started');
}

function sendMessage() {
    const message = document.getElementById('chatInput').value;
    if (message) {
        appendMessage(true, message);
        document.getElementById('chatInput').value = '';
        connection.send("Chat", conversationId, realUserName, message);
    }
}

function appendMessage(isSender, message) {
    const chatMessages = document.getElementById('chatMessages');
    const messageElement = createMessageElement(message, isSender, null)
    chatMessages.appendChild(messageElement);
    chatMessages.scrollTop = chatMessages.scrollHeight;
}

function renderMarkdown(text) {
    if (typeof marked !== 'undefined') {
        const html = marked.parse(text);
        if (typeof DOMPurify !== 'undefined') {
            return DOMPurify.sanitize(html);
        }
        return html;
    }
    // 兜底：未加载 marked 时转义为纯文本
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

function isNearBottom(container) {
    // 距离底部 60px 以内视为"在底部"，此时才自动跟随滚动
    return container.scrollHeight - container.scrollTop - container.clientHeight < 60;
}

function appendMessageWithId(id, message) {
    // We update the full message
    const chatMessages = document.getElementById('chatMessages');
    const stickToBottom = isNearBottom(chatMessages);
    if (document.getElementById(id)) {
        let messageElement = document.getElementById(id);
        messageElement.innerHTML = renderMarkdown(message);
    } else {
        let messageElement = createMessageElement(message, false, id);
        chatMessages.appendChild(messageElement);
    }
    // 仅当用户在底部附近时才自动滚动到底，避免打断向上翻阅
    if (stickToBottom) {
        chatMessages.scrollTop = chatMessages.scrollHeight;
    }
}

function createMessageElement(message, isSender, id) {
    const messageElement = document.createElement('div');
    messageElement.classList.add('message', 'markdown-body', isSender ? 'sent' : 'received');
    messageElement.innerHTML = renderMarkdown(message);
    if (id) {
        messageElement.id = id;
    }
    return messageElement;
}

function updateConnectionStatus(isConnected) {
    const statusElement = document.getElementById('connectionStatus');
    if (isConnected) {
        statusElement.innerText = 'Connected';
        statusElement.classList.remove('status-disconnected');
        statusElement.classList.add('status-connected');
    } else {
        statusElement.innerText = 'Disconnected';
        statusElement.classList.remove('status-connected');
        statusElement.classList.add('status-disconnected');
    }
}