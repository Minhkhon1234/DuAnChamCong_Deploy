document.addEventListener('DOMContentLoaded', () => {
    const token = sessionStorage.getItem('token');
    if (!token) {
        window.location.href = '/';
        return;
    }

    const toast = document.getElementById('toast');
    const loadingOverlay = document.getElementById('loadingOverlay');
    const leaveHistoryTableBody = document.getElementById('leaveHistoryTableBody');
    const leaveRequestModal = document.getElementById('leaveRequestModal');
    const btnOpenLeaveModal = document.getElementById('btnOpenLeaveModal');
    const btnCancelLeave = document.getElementById('btnCancelLeave');
    const leaveRequestForm = document.getElementById('leaveRequestForm');

    function showToast(message, type = 'success') {
        toast.textContent = message;
        toast.className = `toast-message show ${type}`;
        setTimeout(() => toast.classList.remove('show'), 3000);
    }

    function showLoading(show) {
        loadingOverlay.style.display = show ? 'flex' : 'none';
    }

    async function loadLeaveHistory() {
        showLoading(true);
        try {
            const response = await fetch('/api/leaverequest/my', {
                headers: { 'Authorization': `Bearer ${token}` }
            });

            if (response.ok) {
                const data = await response.json();
                renderLeaveHistory(data);
            }
        } catch (error) {
            console.error(error);
        } finally {
            showLoading(false);
        }
    }

    function renderLeaveHistory(data) {
        leaveHistoryTableBody.innerHTML = '';
        if (!data || data.length === 0) {
            leaveHistoryTableBody.innerHTML = '<tr><td colspan="4" style="text-align: center;">Chưa có đơn xin nghỉ nào</td></tr>';
            return;
        }

        data.forEach(item => {
            const tr = document.createElement('tr');
            let statusColor = '#f59e0b'; // Pending
            if (item.status === 'Approved') statusColor = '#10b981';
            if (item.status === 'Rejected') statusColor = '#ef4444';

            tr.innerHTML = `
                <td style="text-align: center;"><strong>${new Date(item.leaveDate).toLocaleDateString('vi-VN')}</strong></td>
                <td><div style="font-size: 13px; color: #666;">${item.reason}</div></td>
                <td style="text-align: center;">
                    <span style="background: ${statusColor}; color: white; padding: 4px 10px; border-radius: 20px; font-size: 12px; font-weight: bold;">
                        ${item.status}
                    </span>
                </td>
                <td style="text-align: center; color: #999; font-size: 12px;">${new Date(item.createdAt).toLocaleDateString('vi-VN')}</td>
            `;
            leaveHistoryTableBody.appendChild(tr);
        });
    }

    if (btnOpenLeaveModal) {
        btnOpenLeaveModal.addEventListener('click', () => {
            leaveRequestModal.style.display = 'flex';
            document.getElementById('leaveDate').valueAsDate = new Date();
        });
    }

    if (btnCancelLeave) {
        btnCancelLeave.addEventListener('click', () => {
            leaveRequestModal.style.display = 'none';
            leaveRequestForm.reset();
        });
    }

    if (leaveRequestForm) {
        leaveRequestForm.addEventListener('submit', async (e) => {
            e.preventDefault();
            const formData = new FormData();
            formData.append('leaveDate', document.getElementById('leaveDate').value);
            formData.append('reason', document.getElementById('leaveReason').value);
            const imageFile = document.getElementById('leaveImage').files[0];
            if (imageFile) formData.append('image', imageFile);

            showLoading(true);
            try {
                const response = await fetch('/api/leaverequest', {
                    method: 'POST',
                    headers: { 'Authorization': `Bearer ${token}` },
                    body: formData
                });

                if (response.ok) {
                    const result = await response.json();
                    showToast(result.message, "success");
                    leaveRequestModal.style.display = 'none';
                    leaveRequestForm.reset();
                    loadLeaveHistory();
                } else {
                    showToast("Lỗi gửi yêu cầu", "error");
                }
            } catch (error) {
                showToast("Lỗi kết nối máy chủ", "error");
            } finally {
                showLoading(false);
            }
        });
    }

    loadLeaveHistory();
});
