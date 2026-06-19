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

    const btnLogout = document.getElementById('btnLogout');
    const btnBackToDashboard = document.getElementById('btnBackToDashboard');
    const monthlyTableBody = document.getElementById('monthlyTableBody');
    const btnFilterMonthly = document.getElementById('btnFilterMonthly');
    const monthSelect = document.getElementById('monthSelect');
    const yearSelect = document.getElementById('yearSelect');
    const toast = document.getElementById('toast');
    const loadingOverlay = document.getElementById('loadingOverlay');

    function showToast(message, type = 'success') {
        toast.textContent = message;
        toast.className = `toast-message show ${type}`;
        setTimeout(() => {
            toast.classList.remove('show');
        }, 3000);
    }

    function showLoading(show) {
        loadingOverlay.style.display = show ? 'flex' : 'none';
    }

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

    // Generate Years
    function populateYears() {
        const currentYear = new Date().getFullYear();
        for (let i = currentYear - 2; i <= currentYear + 1; i++) {
            const option = document.createElement('option');
            option.value = i;
            option.textContent = `Năm ${i}`;
            if (i === currentYear) option.selected = true;
            yearSelect.appendChild(option);
        }
        monthSelect.value = new Date().getMonth() + 1;
    }

    // Fetch Monthly Data
    async function fetchMonthlyData(month, year) {
        showLoading(true);
        try {
            const response = await fetch(`/api/dashboard/monthly?month=${month}&year=${year}`, {
                headers: { 'Authorization': `Bearer ${token}` }
            });

            if (response.status === 401 || response.status === 403) {
                showToast("Bạn không có quyền truy cập", "error");
                setTimeout(() => window.location.href = '/', 1500);
                return;
            }

            if (response.ok) {
                const data = await response.json();
                renderMonthlyTable(data);
            } else {
                showToast("Không thể tải báo cáo tháng", "error");
            }
        } catch (error) {
            console.error("Lỗi:", error);
            showToast("Lỗi kết nối máy chủ", "error");
        } finally {
            showLoading(false);
        }
    }

    function renderMonthlyTable(data) {
        monthlyTableBody.innerHTML = '';
        if (!data || data.length === 0) {
            monthlyTableBody.innerHTML = '<tr><td colspan="9" style="text-align: center;">Chưa có dữ liệu</td></tr>';
            return;
        }

        data.forEach(item => {
            const tr = document.createElement('tr');
            tr.innerHTML = `
                <td><strong>${item.fullName}</strong></td>
                <td>${item.email}</td>
                <td style="text-align: center;">
                    <span style="background: #e9ecef; padding: 4px 12px; border-radius: 20px; font-weight: bold;">
                        ${item.totalDays}
                    </span>
                </td>
                <td style="text-align: center; color: #28a745; font-weight: bold;">
                    ${item.totalHours.toFixed(2)}
                </td>
                <td style="text-align: center; color: #28a745; font-weight: bold;">${item.totalOnTime}</td>
                <td style="text-align: center; color: #ffc107; font-weight: bold;">${item.totalLate}</td>
                <td style="text-align: center; color: #fd7e14; font-weight: bold;">${item.totalEarlyLeave}</td>
                <td style="text-align: center; color: #dc3545; font-weight: bold;">${item.totalInvalid}</td>
                <td style="text-align: center; color: #6c757d; font-weight: bold;">${item.totalForgetCheckOut}</td>
            `;
            monthlyTableBody.appendChild(tr);
        });
    }

    // Fetch Monthly Grid
    async function fetchMonthlyGrid(month, year) {
        try {
            const response = await fetch(`/api/dashboard/monthly-grid?month=${month}&year=${year}`, {
                headers: { 'Authorization': `Bearer ${token}` }
            });
            if (response.ok) {
                const data = await response.json();
                renderMonthlyGrid(data);
            }
        } catch (error) {
            console.error("Lỗi grid:", error);
        }
    }

    function renderMonthlyGrid(data) {
        const gridTableHeader = document.getElementById('gridTableHeader');
        const gridTableBody = document.getElementById('gridTableBody');
        
        // Header
        let headerHtml = '<tr><th style="min-width: 150px;">Nhân viên</th>';
        for (let i = 1; i <= data.daysInMonth; i++) {
            headerHtml += `<th style="text-align: center; min-width: 30px;">${i}</th>`;
        }
        headerHtml += '</tr>';
        gridTableHeader.innerHTML = headerHtml;

        // Body
        gridTableBody.innerHTML = '';
        data.userAttendance.forEach(user => {
            const tr = document.createElement('tr');
            let bodyHtml = `<td><strong>${user.fullName}</strong></td>`;
            
            user.dailyStatus.forEach(status => {
                let symbol = '-';
                let color = '#ccc';
                let title = 'Vắng';

                if (status === 'OnTime') {
                    symbol = 'X';
                    color = '#28a745';
                    title = 'Đúng giờ';
                } else if (status === 'Late') {
                    symbol = 'M';
                    color = '#ffc107';
                    title = 'Đi muộn';
                } else if (status === 'ForgetCheckOut') {
                    symbol = 'Q';
                    color = '#dc3545';
                    title = 'Chưa Check-out';
                } else if (status === 'Invalid') {
                    symbol = '!';
                    color = '#dc3545';
                    title = 'Sai vị trí';
                }

                bodyHtml += `<td style="text-align: center; color: ${color}; font-weight: bold;" title="${title}">${symbol}</td>`;
            });
            
            tr.innerHTML = bodyHtml;
            gridTableBody.appendChild(tr);
        });
    }

    async function refreshAll(month, year) {
        await fetchMonthlyData(month, year);
        await fetchMonthlyGrid(month, year);
    }

    btnFilterMonthly.addEventListener('click', () => {
        const month = monthSelect.value;
        const year = yearSelect.value;
        refreshAll(month, year);
    });

    const btnExportExcel = document.getElementById('btnExportExcel');
    if (btnExportExcel) {
        btnExportExcel.addEventListener('click', () => {
            const tables = document.querySelectorAll('.admin-table');
            if (tables.length === 0) {
                showToast("Không có dữ liệu để xuất!", "error");
                return;
            }

            try {
                // Tạo một workbook mới
                var wb = XLSX.utils.book_new();

                // Bảng 1: Báo cáo tính lương tổng hợp
                if (tables[0]) {
                    var ws1 = XLSX.utils.table_to_sheet(tables[0]);
                    XLSX.utils.book_append_sheet(wb, ws1, "TongHopLuong");
                }

                // Bảng 2: Bảng công chi tiết theo ngày
                if (tables.length > 1 && tables[1]) {
                    var ws2 = XLSX.utils.table_to_sheet(tables[1]);
                    XLSX.utils.book_append_sheet(wb, ws2, "ChiTietNgay");
                }
                
                // Đặt tên file theo tháng/năm
                const month = monthSelect.value;
                const year = yearSelect.value;
                const fileName = `BaoCaoThang_${month}_${year}.xlsx`;
                
                // Tải xuống
                XLSX.writeFile(wb, fileName);
                showToast("Xuất file Excel thành công!", "success");
            } catch (error) {
                console.error("Lỗi xuất Excel:", error);
                showToast("Đã xảy ra lỗi khi xuất file Excel", "error");
            }
        });
    }

    // Init
    populateYears();
    refreshAll(monthSelect.value, yearSelect.value);
});
