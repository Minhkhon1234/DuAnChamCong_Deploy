document.addEventListener('DOMContentLoaded', () => {
    const token = sessionStorage.getItem('token');
    if (!token) {
        window.location.href = '/';
        return;
    }

    const toast = document.getElementById('toast');
    const loadingOverlay = document.getElementById('loadingOverlay');
    const historyTableBody = document.getElementById('historyTableBody');

    function showToast(message, type = 'success') {
        toast.textContent = message;
        toast.className = `toast-message show ${type}`;
        setTimeout(() => toast.classList.remove('show'), 3000);
    }

    function showLoading(show) {
        loadingOverlay.style.display = show ? 'flex' : 'none';
    }

    function formatDate(dateString) {
        if (!dateString) return '--';
        const date = new Date(dateString);
        return date.toLocaleString('vi-VN', {
            day: '2-digit', month: '2-digit', year: 'numeric',
            hour: '2-digit', minute: '2-digit'
        });
    }

    function formatStatus(status) {
        if (!status) return '--';
        let badges = '';
        if (status.includes('OnTime')) badges += '<span class="status-badge status-ontime">Đúng giờ</span> ';
        if (status.includes('Late')) badges += '<span class="status-badge status-late">Đi muộn</span> ';
        if (status.includes('EarlyLeave')) badges += '<span class="status-badge status-earlyleave">Về sớm</span> ';
        if (status.includes('InvalidLocation')) badges += '<span class="status-badge status-invalid">Sai vị trí</span> ';
        if (status.includes('ForgetCheckOut')) badges += '<span class="status-badge" style="background:#dc3545;color:white;border:none;">Chưa Check-out</span> ';
        return badges || status;
    }

    async function loadHistory() {
        showLoading(true);
        try {
            const response = await fetch('/api/attendance/history', {
                headers: { 'Authorization': `Bearer ${token}` }
            });

            if (response.ok) {
                const data = await response.json();
                
                document.getElementById('totalShifts').textContent = data.summary.totalShifts;
                document.getElementById('onTimeCount').textContent = data.summary.onTimeCount;
                document.getElementById('lateCount').textContent = data.summary.lateCount;

                const historySection = document.getElementById('historySection');
                const requestDetailSection = document.getElementById('requestDetailSection');
                const btnRequestDetails = document.getElementById('btnRequestDetails');
                const requestStatusText = document.getElementById('requestStatusText');

                if (data.canViewDetails) {
                    historySection.style.display = 'block';
                    requestDetailSection.style.display = 'none';
                    renderHistory(data.details);
                } else {
                    historySection.style.display = 'none';
                    requestDetailSection.style.display = 'block';
                    if (data.requestViewDetails) {
                        btnRequestDetails.disabled = true;
                        btnRequestDetails.textContent = "Đã gửi yêu cầu - Đang chờ duyệt";
                        btnRequestDetails.style.background = "#6c757d";
                        requestStatusText.textContent = "Yêu cầu của bạn đang được xem xét. Vui lòng quay lại sau.";
                    }
                }
            }
        } catch (error) {
            console.error(error);
            showToast("Lỗi tải lịch sử", "error");
        } finally {
            showLoading(false);
        }
    }

    function renderHistory(data) {
        historyTableBody.innerHTML = '';
        if (!data || data.length === 0) {
            historyTableBody.innerHTML = '<tr><td colspan="7" style="text-align: center;">Chưa có dữ liệu điểm danh</td></tr>';
            return;
        }

        data.forEach(item => {
            const tr = document.createElement('tr');
            let statusHtml = formatStatus(item.status);
            if (item.earlyLeaveReason) {
                statusHtml += `<div style="font-size:11px; color:#666; margin-top:4px;">Lý do: ${item.earlyLeaveReason}</div>`;
            }
            tr.innerHTML = `
                <td><strong>${item.schoolName || '--'}</strong></td>
                <td><span class="shift-badge">${item.selectedShifts || '--'}</span></td>
                <td>${formatDate(item.checkInTime)}</td>
                <td>${formatDate(item.checkOutTime)}</td>
                <td>${item.totalHours ? item.totalHours.toFixed(2) + ' giờ' : '--'}</td>
                <td>${statusHtml}</td>
                <td><small>${item.latitude ? item.latitude.toFixed(4) : '--'}, ${item.longitude ? item.longitude.toFixed(4) : '--'}</small></td>
            `;
            historyTableBody.appendChild(tr);
        });
    }

    const btnRequestDetails = document.getElementById('btnRequestDetails');
    if (btnRequestDetails) {
        btnRequestDetails.addEventListener('click', async () => {
            showLoading(true);
            try {
                const response = await fetch('/api/attendance/request-details', {
                    method: 'POST',
                    headers: { 'Authorization': `Bearer ${token}` }
                });
                const result = await response.json();
                if (response.ok) {
                    showToast(result.message, "success");
                    loadHistory();
                } else {
                    showToast(result.message || "Lỗi gửi yêu cầu", "error");
                }
            } catch (error) {
                showToast("Lỗi kết nối máy chủ", "error");
            } finally {
                showLoading(false);
            }
        });
    }

    loadHistory();
});
