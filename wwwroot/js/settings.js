document.addEventListener('DOMContentLoaded', () => {
    const token = sessionStorage.getItem('token');
    if (!token) {
        window.location.href = '/';
        return;
    }

    const toast = document.getElementById('toast');
    const loadingOverlay = document.getElementById('loadingOverlay');
    const settingsForm = document.getElementById('settingsForm');

    function showToast(message, type = 'success') {
        toast.textContent = message;
        toast.className = `toast-message show ${type}`;
        setTimeout(() => toast.classList.remove('show'), 3000);
    }

    function showLoading(show) {
        loadingOverlay.style.display = show ? 'flex' : 'none';
    }

    // Load Profile Data
    async function loadProfile() {
        showLoading(true);
        try {
            const response = await fetch('/api/profile', {
                headers: { 'Authorization': `Bearer ${token}` }
            });
            if (response.ok) {
                const user = await response.json();
                document.getElementById('settingFullName').value = user.fullName;
                document.getElementById('settingEmail').value = user.email;
            } else {
                showToast("Lỗi tải thông tin tài khoản", "error");
            }
        } catch (error) {
            showToast("Lỗi kết nối máy chủ", "error");
        } finally {
            showLoading(false);
        }
    }

    // Handle Submit
    settingsForm.addEventListener('submit', async (e) => {
        e.preventDefault();

        const email = document.getElementById('settingEmail').value;
        const currentPassword = document.getElementById('currentPassword').value;
        const newPassword = document.getElementById('newPassword').value;
        const confirmPassword = document.getElementById('confirmPassword').value;

        if (newPassword && newPassword !== confirmPassword) {
            showToast("Mật khẩu mới không khớp!", "error");
            return;
        }

        if (newPassword && !currentPassword) {
            showToast("Vui lòng nhập mật khẩu hiện tại để đổi mật khẩu!", "error");
            return;
        }

        showLoading(true);
        try {
            const response = await fetch('/api/profile', {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify({
                    email: email,
                    currentPassword: currentPassword,
                    newPassword: newPassword
                })
            });

            const result = await response.json();
            if (response.ok) {
                showToast(result.message, "success");
                // Update session storage if email changed
                sessionStorage.setItem('email', email);

                // Clear password fields
                document.getElementById('currentPassword').value = '';
                document.getElementById('newPassword').value = '';
                document.getElementById('confirmPassword').value = '';
            } else {
                showToast(result.message || "Cập nhật thất bại", "error");
            }
        } catch (error) {
            showToast("Lỗi kết nối máy chủ", "error");
        } finally {
            showLoading(false);
        }
    });

    loadProfile();
});
