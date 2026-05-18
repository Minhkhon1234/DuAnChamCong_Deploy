document.addEventListener('DOMContentLoaded', () => {
    const token = sessionStorage.getItem('token');
    if (!token) {
        window.location.href = '/';
        return;
    }

    const role = sessionStorage.getItem('role');
    if (role !== 'Admin' && role !== 'Leader') {
        window.location.href = '/Home/Dashboard';
        return;
    }

    const fullName = sessionStorage.getItem('fullName');
    if (fullName) {
        const adminNameDisplay = document.getElementById('adminNameDisplay');
        if (adminNameDisplay) adminNameDisplay.textContent = `Xin chào, ${fullName}`;
    }
    
    const roleBadge = document.getElementById('userRoleBadge');
    if (roleBadge) roleBadge.textContent = role;

    // Elements
    const btnLogout = document.getElementById('btnLogout');
    const btnBackToDashboard = document.getElementById('btnBackToDashboard');
    const userTableBody = document.getElementById('userTableBody');
    const toast = document.getElementById('toast');
    const loadingOverlay = document.getElementById('loadingOverlay');
    
    // Modal Elements
    const userModal = document.getElementById('userModal');
    const btnOpenAddModal = document.getElementById('btnOpenAddModal');
    const btnCloseModal = document.getElementById('btnCloseModal');
    const btnCancelModal = document.getElementById('btnCancelModal');
    const userForm = document.getElementById('userForm');
    const modalTitle = document.getElementById('modalTitle');
    
    // Form Inputs
    const inputId = document.getElementById('userId');
    const inputFullName = document.getElementById('fullName');
    const inputEmail = document.getElementById('email');
    const inputPassword = document.getElementById('password');
    const inputRole = document.getElementById('role');
    const passwordHelp = document.getElementById('passwordHelp');

    let currentUsers = [];

    // Utility: Show Toast
    function showToast(message, type = 'success') {
        toast.textContent = message;
        toast.className = `toast-message show ${type}`;
        setTimeout(() => {
            toast.classList.remove('show');
        }, 3000);
    }

    // Utility: Show/Hide Loading
    function showLoading(show) {
        loadingOverlay.style.display = show ? 'flex' : 'none';
    }

    // Logout
    if (btnLogout) {
        btnLogout.addEventListener('click', () => {
            sessionStorage.removeItem('token');
            sessionStorage.removeItem('role');
            sessionStorage.removeItem('fullName');
            window.location.href = '/';
        });
    }

    if (btnBackToDashboard) {
        btnBackToDashboard.addEventListener('click', () => {
            window.location.href = '/Home/AdminDashboard';
        });
    }

    // Load Users
    async function loadUsers() {
        showLoading(true);
        try {
            const response = await fetch('/api/user', {
                headers: { 'Authorization': `Bearer ${token}` }
            });

            if (response.status === 401 || response.status === 403) {
                showToast("Bạn không có quyền truy cập", "error");
                setTimeout(() => window.location.href = '/', 1500);
                return;
            }

            if (response.ok) {
                currentUsers = await response.json();
                renderUsers(currentUsers);
            } else {
                showToast("Lỗi tải danh sách người dùng", "error");
            }
        } catch (error) {
            console.error(error);
            showToast("Lỗi kết nối máy chủ", "error");
        } finally {
            showLoading(false);
        }
    }

    // Render Table
    function renderUsers(users) {
        userTableBody.innerHTML = '';
        if (users.length === 0) {
            userTableBody.innerHTML = '<tr><td colspan="5" style="text-align:center">Chưa có người dùng nào</td></tr>';
            return;
        }

        users.forEach(u => {
            const tr = document.createElement('tr');
            
            let statusHtml = u.isActive 
                ? '<span class="status-active">Đang hoạt động</span>' 
                : '<span class="status-inactive">Chờ duyệt / Khóa</span>';

            let actionsHtml = `<button class="btn-action btn-edit" onclick="editUser(${u.id})">Sửa</button>`;
            
            if (u.isActive) {
                actionsHtml += `<button class="btn-action btn-delete" onclick="deleteUser(${u.id})">Khóa</button>`;
            } else {
                actionsHtml += `<button class="btn-action btn-restore" onclick="restoreUser(${u.id})">Duyệt / Mở khóa</button>`;
            }

            // Bổ sung nút duyệt xem chi tiết
            let detailRequestHtml = '';
            if (u.canViewDetails) {
                detailRequestHtml = `<span class="status-active">Đã duyệt</span> <button class="btn-action btn-delete" style="padding: 2px 8px; font-size: 11px;" onclick="revokeDetail(${u.id})">Khóa lại</button>`;
            } else if (u.requestViewDetails) {
                detailRequestHtml = `<span class="status-inactive">Đang yêu cầu</span> <button class="btn-action btn-restore" style="padding: 2px 8px; font-size: 11px;" onclick="approveDetail(${u.id})">Duyệt ngay</button>`;
            } else {
                detailRequestHtml = '<small style="color:#999">Chưa yêu cầu</small>';
            }

            tr.innerHTML = `
                <td><strong>${u.fullName}</strong></td>
                <td>${u.email}</td>
                <td style="text-align:center">${u.role}</td>
                <td style="text-align:center">${statusHtml}</td>
                <td style="text-align:center">${detailRequestHtml}</td>
                <td style="text-align:center">${actionsHtml}</td>
            `;
            userTableBody.appendChild(tr);
        });
    }

    // ================= MODAL LOGIC =================
    function openModal(isEdit = false) {
        userModal.style.display = 'flex';
        if (!isEdit) {
            modalTitle.textContent = "Thêm Nhân Viên Mới";
            userForm.reset();
            inputId.value = "";
            inputPassword.required = true;
            passwordHelp.style.display = 'none';
        } else {
            modalTitle.textContent = "Sửa Thông Tin Nhân Viên";
            inputPassword.required = false;
            passwordHelp.style.display = 'block';
        }
    }

    function closeModal() {
        userModal.style.display = 'none';
    }

    btnOpenAddModal.addEventListener('click', () => openModal(false));
    btnCloseModal.addEventListener('click', closeModal);
    btnCancelModal.addEventListener('click', closeModal);

    // Click ra ngoài để đóng modal
    window.addEventListener('click', (e) => {
        if (e.target === userModal) closeModal();
    });

    // ================= CRUD ACTIONS =================
    
    // Submit Form (Add or Edit)
    userForm.addEventListener('submit', async (e) => {
        e.preventDefault();
        
        const id = inputId.value;
        const isEdit = id !== "";
        const url = isEdit ? `/api/user/${id}` : `/api/user`;
        const method = isEdit ? 'PUT' : 'POST';

        const payload = {
            fullName: inputFullName.value,
            email: inputEmail.value,
            role: inputRole.value,
            password: inputPassword.value
        };

        if (isEdit && !payload.password) {
            delete payload.password; // Không gửi password nếu trống để backend tự hiểu
        }

        showLoading(true);
        try {
            const response = await fetch(url, {
                method: method,
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify(payload)
            });

            const result = await response.json();
            if (response.ok) {
                showToast(result.message, "success");
                closeModal();
                loadUsers();
            } else {
                showToast(result.message || "Lưu thất bại", "error");
            }
        } catch (error) {
            showToast("Lỗi kết nối", "error");
        } finally {
            showLoading(false);
        }
    });

    // Edit User (Called from HTML inline onclick)
    window.editUser = function(id) {
        const user = currentUsers.find(u => u.id === id);
        if (!user) return;

        openModal(true);
        inputId.value = user.id;
        inputFullName.value = user.fullName;
        inputEmail.value = user.email;
        inputRole.value = user.role;
        inputPassword.value = ""; // Reset password field
    };

    // Soft Delete User
    window.deleteUser = async function(id) {
        if (!confirm("Bạn có chắc chắn muốn vô hiệu hóa nhân viên này? (Dữ liệu điểm danh cũ sẽ được giữ lại)")) return;

        showLoading(true);
        try {
            const response = await fetch(`/api/user/${id}`, {
                method: 'DELETE',
                headers: { 'Authorization': `Bearer ${token}` }
            });
            const result = await response.json();
            if (response.ok) {
                showToast(result.message, "success");
                loadUsers();
            } else {
                showToast(result.message || "Xóa thất bại", "error");
            }
        } catch (error) {
            showToast("Lỗi kết nối", "error");
        } finally {
            showLoading(false);
        }
    };

    // Restore User
    window.restoreUser = async function(id) {
        if (!confirm("Bạn có chắc chắn muốn duyệt/khôi phục tài khoản này?")) return;

        showLoading(true);
        try {
            const response = await fetch(`/api/user/restore/${id}`, {
                method: 'POST',
                headers: { 'Authorization': `Bearer ${token}` }
            });
            const result = await response.json();
            if (response.ok) {
                showToast(result.message, "success");
                loadUsers();
            } else {
                showToast(result.message || "Khôi phục thất bại", "error");
            }
        } catch (error) {
            showToast("Lỗi kết nối", "error");
        } finally {
            showLoading(false);
        }
    };

    // Detail View Approval
    window.approveDetail = async function(id) {
        showLoading(true);
        try {
            const response = await fetch(`/api/user/approve-detail/${id}`, {
                method: 'POST',
                headers: { 'Authorization': `Bearer ${token}` }
            });
            const result = await response.json();
            if (response.ok) {
                showToast(result.message, "success");
                loadUsers();
            } else {
                showToast(result.message || "Lỗi phê duyệt", "error");
            }
        } catch (error) {
            showToast("Lỗi kết nối", "error");
        } finally {
            showLoading(false);
        }
    };

    window.revokeDetail = async function(id) {
        if (!confirm("Khóa quyền xem chi tiết của nhân viên này?")) return;
        showLoading(true);
        try {
            const response = await fetch(`/api/user/revoke-detail/${id}`, {
                method: 'POST',
                headers: { 'Authorization': `Bearer ${token}` }
            });
            const result = await response.json();
            if (response.ok) {
                showToast(result.message, "success");
                loadUsers();
            } else {
                showToast(result.message || "Lỗi thực hiện", "error");
            }
        } catch (error) {
            showToast("Lỗi kết nối", "error");
        } finally {
            showLoading(false);
        }
    };

    // Init
    loadUsers();
});
