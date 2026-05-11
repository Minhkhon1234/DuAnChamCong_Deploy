document.addEventListener('DOMContentLoaded', () => {
    const token = sessionStorage.getItem('token');
    if (!token) {
        window.location.href = '/';
        return;
    }

    const fullName = sessionStorage.getItem('fullName');
    const role = sessionStorage.getItem('role');
    if (fullName) {
        const adminNameDisplay = document.getElementById('adminNameDisplay');
        if (adminNameDisplay) adminNameDisplay.textContent = `Xin chào, ${fullName}`;
    }

    // Decode JWT to get Role (Optional since auth.js handled it, but good to show role)
    function parseJwt(token) {
        try {
            const base64Url = token.split('.')[1];
            const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
            const jsonPayload = decodeURIComponent(window.atob(base64).split('').map(function (c) {
                return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
            }).join(''));
            return JSON.parse(jsonPayload);
        } catch (e) {
            return null;
        }
    }

    const tokenData = parseJwt(token);
    if (tokenData) {
        // Extract role from standard claims
        const roleClaim = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";
        const role = tokenData[roleClaim] || tokenData.role || "Quản Lý";
        const roleBadge = document.getElementById('userRoleBadge');
        if (roleBadge) roleBadge.textContent = role;
    }

    // Elements
    const btnLogout = document.getElementById('btnLogout');
    const loadingOverlay = document.getElementById('loadingOverlay');
    const toast = document.getElementById('toast');

    // Date Display
    const dateDisplay = document.getElementById('currentDateDisplay');
    const options = { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' };
    dateDisplay.textContent = new Date().toLocaleDateString('vi-VN', options);



    // Show Loading
    function showLoading(show) {
        loadingOverlay.style.display = show ? 'flex' : 'none';
    }

    // Show Toast
    function showToast(message, type = 'error') {
        toast.textContent = message;
        toast.className = `toast-message show ${type}`;
        setTimeout(() => toast.classList.remove('show'), 3000);
    }

    // Fetch Summary
    let dailyDetails = null;

    async function fetchSummary() {
        showLoading(true);
        try {
            const response = await fetch('/api/dashboard/summary', {
                headers: { 'Authorization': `Bearer ${token}` }
            });

            if (response.status === 401 || response.status === 403) {
                localStorage.removeItem('token');
                window.location.href = '/';
                return;
            }

            if (response.ok) {
                const data = await response.json();

                // Update numbers
                document.getElementById('statTotalUsers').textContent = data.summary.totalUsers;
                document.getElementById('statTotalPresent').textContent = data.summary.totalPresentToday;
                document.getElementById('statTotalOnTime').textContent = data.summary.totalOnTimeToday;
                document.getElementById('statTotalLate').textContent = data.summary.totalLateToday;
                document.getElementById('statTotalAbsent').textContent = data.summary.totalAbsentToday;
                document.getElementById('statTotalInvalid').textContent = data.summary.totalInvalidToday;

                // Fetch Leave Requests Count
                fetchLeaveRequestsCount();

                // Save details for click event
                dailyDetails = data.details;

                // Auto load danh sách có mặt hôm nay
                if (dailyDetails && dailyDetails.present) {
                    renderDailyDetail("Danh sách: Có Mặt Hôm Nay", dailyDetails.present);

                    // Kích hoạt style cho thẻ present
                    const presentCard = document.querySelector('.stat-card.present');
                    if (presentCard) presentCard.classList.add('active');
                }
            } else {
                showToast("Không thể lấy dữ liệu thống kê.");
            }
        } catch (error) {
            showToast("Lỗi kết nối tới máy chủ.");
        } finally {
            showLoading(false);
        }
    }

    // Format Date Time
    function formatTime(dateString) {
        if (!dateString) return '--';
        const date = new Date(dateString);
        return date.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
    }

    // Format Status
    function formatDailyStatus(status) {
        if (!status) return '--';
        if (status === 'User') return '<span style="color:var(--primary-color);font-weight:600;">Nhân viên</span>';
        if (status === 'Absent') return '<span style="color:var(--danger-color);font-weight:600;">Chưa điểm danh</span>';
        let badges = '';
        if (status.includes('OnTime')) badges += '<span style="color:var(--success-color);font-weight:600;">Đúng giờ</span> ';
        if (status.includes('Late')) badges += '<span style="color:var(--warning-color);font-weight:600;">Đi muộn</span> ';
        if (status.includes('EarlyLeave')) badges += '<span style="color:var(--danger-color);font-weight:600;">Về sớm</span> ';
        if (status.includes('InvalidLocation')) badges += '<span style="color:var(--text-muted);font-weight:600;">Sai vị trí</span> ';
        return badges || status;
    }

    // Render Daily Detail Table
    const dailyDetailSection = document.getElementById('dailyDetailSection');
    const dailyDetailTitle = document.getElementById('dailyDetailTitle');
    const dailyDetailTableBody = document.getElementById('dailyDetailTableBody');

    function renderDailyDetail(title, dataList) {
        dailyDetailSection.style.display = 'block';
        dailyDetailTitle.textContent = title;
        dailyDetailTableBody.innerHTML = '';

        if (!dataList || dataList.length === 0) {
            dailyDetailTableBody.innerHTML = '<tr><td colspan="4" style="text-align: center;">Không có dữ liệu</td></tr>';
            return;
        }

        dataList.forEach(item => {
            const tr = document.createElement('tr');
            let reasonText = item.earlyLeaveReason ? `<div style="font-size:12px; color:#666; max-width: 200px; line-height: 1.2;">${item.earlyLeaveReason}</div>` : '--';
            tr.innerHTML = `
                <td><strong>${item.fullName}</strong></td>
                <td>${item.email}</td>
                <td><strong>${item.schoolName || '--'}</strong></td>
                <td><span class="shift-badge">${item.selectedShifts || '--'}</span></td>
                <td style="text-align: center;">${formatTime(item.time)}</td>
                <td style="text-align: center;">${formatDailyStatus(item.status)}</td>
                <td style="text-align: center;">${reasonText}</td>
            `;
            dailyDetailTableBody.appendChild(tr);
        });
    }

    // ================= LEAVE REQUESTS LOGIC =================
    const leaveRequestSection = document.getElementById('leaveRequestSection');
    const leaveRequestTableBody = document.getElementById('leaveRequestTableBody');

    async function fetchLeaveRequestsCount() {
        try {
            const response = await fetch('/api/leaverequest/admin', {
                headers: { 'Authorization': `Bearer ${token}` }
            });
            if (response.ok) {
                const data = await response.json();
                const pendingCount = data.filter(r => r.status === 'Pending').length;
                document.getElementById('statTotalLeave').textContent = pendingCount;
                
                // Store globally for rendering
                window.allLeaveRequests = data;
            }
        } catch (error) {
            console.error("Lỗi fetch leave count:", error);
        }
    }

    function renderLeaveRequests() {
        dailyDetailSection.style.display = 'none';
        leaveRequestSection.style.display = 'block';
        leaveRequestTableBody.innerHTML = '';

        const data = window.allLeaveRequests || [];
        if (data.length === 0) {
            leaveRequestTableBody.innerHTML = '<tr><td colspan="6" style="text-align: center;">Không có yêu cầu xin nghỉ</td></tr>';
            return;
        }

        data.forEach(item => {
            const tr = document.createElement('tr');
            
            let imgHtml = item.imagePath 
                ? `<img src="${item.imagePath}" style="width: 50px; height: 50px; object-fit: cover; border-radius: 4px; cursor: pointer;" onclick="previewImage('${item.imagePath}')">` 
                : '<small style="color:#999">Không có ảnh</small>';
            
            let statusBadge = `<span class="role-badge" style="background: ${getStatusColor(item.status)};">${item.status}</span>`;
            
            let actionsHtml = '';
            if (item.status === 'Pending') {
                actionsHtml = `
                    <button class="btn-filter" style="background: var(--success-color); padding: 5px 10px; font-size: 12px;" onclick="handleLeave(${item.id}, 'Approved')">Duyệt</button>
                    <button class="btn-filter" style="background: var(--danger-color); padding: 5px 10px; font-size: 12px;" onclick="handleLeave(${item.id}, 'Rejected')">Từ chối</button>
                `;
            } else {
                actionsHtml = '<small style="color:#999">Đã xử lý</small>';
            }

            tr.innerHTML = `
                <td><strong>${item.userFullName}</strong></td>
                <td style="text-align: center;">${new Date(item.leaveDate).toLocaleDateString('vi-VN')}</td>
                <td><div style="max-width: 300px; font-size: 13px;">${item.reason}</div></td>
                <td style="text-align: center;">${imgHtml}</td>
                <td style="text-align: center;">${statusBadge}</td>
                <td style="text-align: center;">${actionsHtml}</td>
            `;
            leaveRequestTableBody.appendChild(tr);
        });
    }

    function getStatusColor(status) {
        switch (status) {
            case 'Pending': return '#f59e0b';
            case 'Approved': return '#10b981';
            case 'Rejected': return '#ef4444';
            default: return '#ccc';
        }
    }

    window.handleLeave = async function(id, status) {
        showLoading(true);
        try {
            const response = await fetch(`/api/leaverequest/handle/${id}`, {
                method: 'POST',
                headers: { 
                    'Authorization': `Bearer ${token}`,
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(status)
            });
            if (response.ok) {
                showToast("Đã cập nhật trạng thái yêu cầu", "success");
                await fetchLeaveRequestsCount(); // Refresh data
                renderLeaveRequests();
            } else {
                showToast("Lỗi khi xử lý yêu cầu", "error");
            }
        } catch (error) {
            showToast("Lỗi kết nối", "error");
        } finally {
            showLoading(false);
        }
    };

    // Image Preview
    const imageModal = document.getElementById('imageModal');
    const modalImage = document.getElementById('modalImage');
    window.previewImage = function(src) {
        modalImage.src = src;
        imageModal.style.display = 'flex';
    };
    document.getElementById('closeImageModal')?.addEventListener('click', () => {
        imageModal.style.display = 'none';
    });
    imageModal?.addEventListener('click', (e) => {
        if (e.target === imageModal) imageModal.style.display = 'none';
    });

    // Stat Card Click Events
    const statCards = document.querySelectorAll('.stat-card');

    statCards.forEach(card => {
        card.addEventListener('click', () => {
            if (!dailyDetails) return;

            // Remove active class from all
            statCards.forEach(c => c.classList.remove('active'));
            // Add to clicked
            card.classList.add('active');

            if (card.classList.contains('total-users')) {
                renderDailyDetail("Danh sách: Tổng Nhân Sự", dailyDetails.totalUsers);
            } else if (card.classList.contains('present')) {
                renderDailyDetail("Danh sách: Có Mặt Hôm Nay", dailyDetails.present);
            } else if (card.classList.contains('ontime')) {
                renderDailyDetail("Danh sách: Đúng Giờ", dailyDetails.onTime);
            } else if (card.classList.contains('late')) {
                renderDailyDetail("Danh sách: Đi Muộn", dailyDetails.late);
            } else if (card.classList.contains('absent')) {
                renderDailyDetail("Danh sách: Vắng Mặt", dailyDetails.absent);
            } else if (card.classList.contains('invalid')) {
                renderDailyDetail("Danh sách: Sai Vị Trí", dailyDetails.invalid);
            } else if (card.classList.contains('leave-requests')) {
                renderLeaveRequests();
            }
        });
    });

    // Switch back to daily detail when clicking other cards
    statCards.forEach(card => {
        if (!card.classList.contains('leave-requests')) {
            card.addEventListener('click', () => {
                leaveRequestSection.style.display = 'none';
                dailyDetailSection.style.display = 'block';
            });
        }
    });



    // Logout
    btnLogout.addEventListener('click', () => {
        sessionStorage.removeItem('token');
        sessionStorage.removeItem('role');
        sessionStorage.removeItem('fullName');
        window.location.href = '/';
    });

    const btnMonthlyReport = document.getElementById('btnMonthlyReport');
    if (btnMonthlyReport) {
        btnMonthlyReport.addEventListener('click', () => {
            window.location.href = '/Home/MonthlyReport';
        });
    }

    const btnUserManagement = document.getElementById('btnUserManagement');
    if (btnUserManagement) {
        btnUserManagement.addEventListener('click', () => {
            window.location.href = '/Home/UserManagement';
        });
    }

    // Init
    fetchSummary();
});
