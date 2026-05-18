const signUpBtn = document.getElementById('signUpBtn');
const signInBtn = document.getElementById('signInBtn');
const authBox = document.getElementById('authBox');

const loginForm = document.getElementById('loginForm');
const registerForm = document.getElementById('registerForm');

const toast = document.getElementById('toast');

// Animation Toggle
signUpBtn.addEventListener('click', () => {
    authBox.classList.add("right-panel-active");
});

signInBtn.addEventListener('click', () => {
    authBox.classList.remove("right-panel-active");
});

// Show Toast function
function showToast(message, type = 'success') {
    toast.textContent = message;
    toast.className = `toast-message show ${type}`;
    setTimeout(() => {
        toast.classList.remove('show');
    }, 3000);
}

// Login Logic
loginForm.addEventListener('submit', async (e) => {
    e.preventDefault();
    const email = document.getElementById('loginEmail').value;
    const password = document.getElementById('loginPassword').value;

    try {
        const response = await fetch('/api/auth/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email, password })
        });

        const text = await response.text();
        let data;
        try { data = JSON.parse(text); } catch { data = text; }

        if (response.ok) {
            sessionStorage.setItem('token', data.token);
            sessionStorage.setItem('role', data.role);
            sessionStorage.setItem('fullName', data.fullName);

            showToast('Đăng nhập thành công! Đang chuyển hướng...', 'success');

            // Redirect based on role
            setTimeout(() => {
                if (data.role === 'Admin' || data.role === 'Leader') {
                    window.location.href = '/Home/AdminDashboard';
                } else {
                    window.location.href = '/Home/Dashboard';
                }
            }, 1000);
        } else {
            const errorMsg = data.message || (typeof data === 'string' ? data : 'Sai tài khoản hoặc mật khẩu');
            showToast(errorMsg, 'error');
        }
    } catch (err) {
        showToast('Lỗi kết nối máy chủ', 'error');
    }
});

// Register Logic
registerForm.addEventListener('submit', async (e) => {
    e.preventDefault();
    const fullName = document.getElementById('regFullName').value;
    const email = document.getElementById('regEmail').value;
    const password = document.getElementById('regPassword').value;

    try {
        const response = await fetch('/api/auth/register', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ fullName, email, password })
        });

        // Read text because API returns Ok("Đăng ký thành công") which is string
        const text = await response.text();

        if (response.ok) {
            showToast(text || 'Đăng ký thành công!', 'success');
            // Automatically switch to login panel
            setTimeout(() => {
                signInBtn.click();
                document.getElementById('loginEmail').value = email;
            }, 1500);
        } else {
            let errorMsg = text;
            try {
                const json = JSON.parse(text);
                if (json.errors) {
                    errorMsg = Object.values(json.errors).flat().join(', ');
                }
            } catch { }
            showToast(errorMsg || 'Đăng ký thất bại', 'error');
        }
    } catch (err) {
        showToast('Lỗi kết nối máy chủ', 'error');
    }
});

// Auto check if already logged in (optional)
document.addEventListener('DOMContentLoaded', () => {
    const token = sessionStorage.getItem('token');
    if (token) {
        // Already have a token, you can do something here
        console.log("Token exists:", token);
    }
});
