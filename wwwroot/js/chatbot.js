document.addEventListener('DOMContentLoaded', () => {
    const chatbotToggle = document.getElementById('chatbotToggle');
    const chatbotWindow = document.getElementById('chatbotWindow');
    const chatbotClose = document.getElementById('chatbotClose');
    const chatbotMessages = document.getElementById('chatbotMessages');
    const chatbotSuggestions = document.getElementById('chatbotSuggestions');
    const btnHistoryClear = document.getElementById('btnHistoryClear');

    const token = sessionStorage.getItem('token');
    if (!token) return;

    // Toggle Window
    chatbotToggle.addEventListener('click', () => {
        const isVisible = chatbotWindow.style.display === 'flex';
        chatbotWindow.style.display = isVisible ? 'none' : 'flex';
        if (!isVisible) {
            loadChatHistory();
            loadSchools();
        }
    });

    chatbotClose.addEventListener('click', () => {
        chatbotWindow.style.display = 'none';
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
                    loadChatHistory(); // Load lại để kích hoạt chào tự động
                }
            } catch (error) {
                console.error("Lỗi xóa lịch sử:", error);
            }
        });
    }

    function appendMessage(sender, text, className = '') {
        const msgDiv = document.createElement('div');
        msgDiv.className = `message ${sender} ${className}`;
        // Support simple markdown bold
        let htmlText = text.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
        // Support simple markdown links
        htmlText = htmlText.replace(/\[(.*?)\]\((.*?)\)/g, '<a href="$2" target="_blank" rel="noopener noreferrer" style="color: #007bff; text-decoration: underline; font-weight: bold;">$1</a>');
        // Support new lines
        htmlText = htmlText.replace(/\n/g, '<br/>');
        msgDiv.innerHTML = htmlText;
        
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

    async function loadSchools() {
        if (chatbotSuggestions.children.length > 0) return; // Đã load
        try {
            const res = await fetch('/api/chatbot/schools', {
                headers: { 'Authorization': `Bearer ${token}` }
            });
            if (res.ok) {
                const schools = await res.json();
                chatbotSuggestions.innerHTML = '';
                schools.forEach(school => {
                    const btn = document.createElement('button');
                    btn.className = 'suggestion-btn';
                    btn.style.cssText = 'background: #e2e8f0; border: none; padding: 6px 12px; border-radius: 15px; cursor: pointer; font-size: 13px; color: #4a5568; transition: background 0.3s;';
                    btn.textContent = `📍 ${school.name}`;
                    btn.onmouseover = () => btn.style.background = '#cbd5e0';
                    btn.onmouseout = () => btn.style.background = '#e2e8f0';
                    btn.onclick = () => handleSchoolClick(school.id, school.name);
                    chatbotSuggestions.appendChild(btn);
                });
            }
        } catch (e) {
            console.error("Lỗi lấy danh sách trường:", e);
        }
    }

    async function handleSchoolClick(schoolId, schoolName) {
        appendMessage('user', `Cho tôi xem địa chỉ của ${schoolName}`);
        
        const buttons = chatbotSuggestions.querySelectorAll('button');
        buttons.forEach(b => { b.disabled = true; b.style.opacity = '0.5'; });

        const typingId = appendMessage('bot', 'Đang tra cứu dữ liệu từ Bản đồ...', 'typing');

        try {
            const response = await fetch('/api/chatbot/location', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify({ SchoolId: schoolId, SchoolName: schoolName })
            });

            const data = await response.json();
            removeMessage(typingId);

            if (response.ok) {
                appendMessage('bot', data.response);
            } else {
                appendMessage('bot', 'Lỗi: Không thể lấy dữ liệu địa chỉ. Vui lòng thử lại sau.');
            }
        } catch (error) {
            removeMessage(typingId);
            appendMessage('bot', 'Lỗi kết nối máy chủ.');
        } finally {
            buttons.forEach(b => { b.disabled = false; b.style.opacity = '1'; });
        }
    }

    async function loadChatHistory() {
        if (chatbotMessages.children.length > 0) return;

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
                    if (msg && msg !== "[Hệ thống tự động chào hỏi]") {
                        appendMessage('user', msg);
                    }
                    if (res) appendMessage('bot', res);
                });

                if (history.length === 0) {
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
                        }
                    } catch (e) {
                        removeMessage(typingId);
                    }
                }
            }
        } catch (error) {
            console.error("Lỗi tải lịch sử chat:", error);
        }
    }

    // Auto open chatbot on first load after login
    const chatbotOpened = sessionStorage.getItem('chatbotAutoOpened');
    if (!chatbotOpened && sessionStorage.getItem('role') === 'User') {
        sessionStorage.setItem('chatbotAutoOpened', 'true');
        setTimeout(() => {
            chatbotWindow.style.display = 'flex';
            loadChatHistory();
            loadSchools();
        }, 1500);
    }
});
