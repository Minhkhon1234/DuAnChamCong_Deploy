document.addEventListener('DOMContentLoaded', () => {
    const chatbotToggle = document.getElementById('chatbotToggle');
    const chatbotWindow = document.getElementById('chatbotWindow');
    const chatbotClose = document.getElementById('chatbotClose');
    const chatbotForm = document.getElementById('chatbotForm');
    const chatbotInput = document.getElementById('chatbotInput');
    const chatbotMessages = document.getElementById('chatbotMessages');
    const btnHistoryClear = document.getElementById('btnHistoryClear');

    const token = sessionStorage.getItem('token');
    if (!token) return;

    // Toggle Window
    chatbotToggle.addEventListener('click', () => {
        const isVisible = chatbotWindow.style.display === 'flex';
        chatbotWindow.style.display = isVisible ? 'none' : 'flex';
        if (!isVisible) {
            loadChatHistory();
            chatbotInput.focus();
        }
    });

    chatbotClose.addEventListener('click', () => {
        chatbotWindow.style.display = 'none';
    });

    // Send Message
    chatbotForm.addEventListener('submit', async (e) => {
        e.preventDefault();
        const message = chatbotInput.value.trim();
        if (!message) return;

        appendMessage('user', message);
        chatbotInput.value = '';

        // Show typing
        const typingId = appendMessage('bot', '...', 'typing');

        try {
            const response = await fetch('/api/chatbot/query', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify(message)
            });

            const data = await response.json();
            removeMessage(typingId);

            if (response.ok) {
                appendMessage('bot', data.response);
            } else {
                appendMessage('bot', 'Lỗi: Không thể nhận phản hồi. Hãy thử lại hoặc xóa lịch sử chat.');
            }
        } catch (error) {
            removeMessage(typingId);
            appendMessage('bot', 'Lỗi kết nối máy chủ.');
        }
    });

    // Clear History
    if (btnHistoryClear) {
        btnHistoryClear.addEventListener('click', async () => {
            if (!confirm("Bạn có chắc chắn muốn xóa toàn bộ lịch sử trò chuyện không?")) return;

            try {
                const response = await fetch('/api/chatbot/clear', {
                    method: 'DELETE',
                    headers: { 'Authorization': `Bearer ${token}` }
                });
                if (response.ok) {
                    chatbotMessages.innerHTML = '';
                    loadChatHistory(); // Load lại để kích hoạt AI chào tự động
                }
            } catch (error) {
                console.error("Lỗi xóa lịch sử:", error);
            }
        });
    }

    function appendMessage(sender, text, className = '') {
        const msgDiv = document.createElement('div');
        msgDiv.className = `message ${sender} ${className}`;
        msgDiv.textContent = text;
        const id = 'msg-' + Date.now();
        msgDiv.id = id;
        chatbotMessages.appendChild(msgDiv);
        chatbotMessages.scrollTop = chatbotMessages.scrollHeight;
        return id;
    }

    function removeMessage(id) {
        const el = document.getElementById(id);
        if (el) el.remove();
    }

    async function loadChatHistory() {
        if (chatbotMessages.children.length > 0) return; // Already loaded or has active chat

        try {
            const response = await fetch('/api/chatbot/history', {
                headers: { 'Authorization': `Bearer ${token}` }
            });
            if (response.ok) {
                const history = await response.json();
                chatbotMessages.innerHTML = '';
                history.forEach(chat => {
                    const msg = chat.message || chat.Message;
                    const res = chat.response || chat.Response;
                    if (msg) appendMessage('user', msg);
                    if (res) appendMessage('bot', res);
                });

                if (history.length === 0) {
                    const role = sessionStorage.getItem('role');
                    if (role === 'User') {
                        const typingId = appendMessage('bot', '...', 'typing');
                        try {
                            const greetRes = await fetch('/api/chatbot/greeting', {
                                method: 'POST',
                                headers: { 'Authorization': `Bearer ${token}` }
                            });
                            removeMessage(typingId);
                            if (greetRes.ok) {
                                const greetData = await greetRes.json();
                                appendMessage('bot', greetData.response);
                            } else {
                                appendMessage('bot', 'Xin chào! Tôi có thể giúp gì cho bạn hôm nay?');
                            }
                        } catch (e) {
                            removeMessage(typingId);
                            appendMessage('bot', 'Xin chào! Tôi có thể giúp gì cho bạn hôm nay?');
                        }
                    } else {
                        // Admin/Leader skip greeting fetch
                        appendMessage('bot', 'Xin chào Quản lý! Tôi có thể giúp gì cho bạn hôm nay?');
                    }
                }
            }
        } catch (error) {
            console.error("Lỗi tải lịch sử chat:", error);
        }
    }
});
