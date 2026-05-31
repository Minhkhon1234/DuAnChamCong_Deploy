document.addEventListener('DOMContentLoaded', () => {
    const token = sessionStorage.getItem('token');
    if (!token) {
        window.location.href = '/';
        return;
    }

    const fullName = sessionStorage.getItem('fullName');
    if (fullName) {
        const userNameDisplay = document.getElementById('userNameDisplay');
        if (userNameDisplay) userNameDisplay.textContent = `Xin chào, ${fullName}`;
    }

    // Get or Create DeviceId for Anti-Spoofing
    let deviceId = localStorage.getItem('deviceId');
    if (!deviceId) {
        deviceId = 'device-' + Math.random().toString(36).substr(2, 9) + '-' + Date.now();
        localStorage.setItem('deviceId', deviceId);
    }

    // Elements
    const btnCheckIn = document.getElementById('btnCheckIn');
    const btnCheckOut = document.getElementById('btnCheckOut');
    const toast = document.getElementById('toast');
    const loadingOverlay = document.getElementById('loadingOverlay');
    const schoolSelect = document.getElementById('schoolSelect');

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
        if (show) {
            loadingOverlay.style.display = 'flex';
        } else {
            loadingOverlay.style.display = 'none';
        }
    }

    // State
    let lastFetchedHistory = [];
    let currentOpenRecord = null;

    // Load State
    async function loadState() {
        try {
            const response = await fetch('/api/attendance/history', {
                headers: { 'Authorization': `Bearer ${token}` }
            });

            if (response.status === 401) {
                sessionStorage.removeItem('token');
                window.location.href = '/';
                return;
            }

            if (response.ok) {
                const data = await response.json();
                lastFetchedHistory = data.details || [];
                currentOpenRecord = data.todayOpenRecord;
                updateButtonStates();
            }
        } catch (error) {
            console.error("Lỗi tải trạng thái:", error);
        } finally {
            showLoading(false);
        }
    }

    // Listen to School selection change
    schoolSelect.addEventListener('change', () => {
        updateButtonStates();
    });

    // Update Button States
    function updateButtonStates() {
        const selectedSchoolName = schoolSelect.options[schoolSelect.selectedIndex]?.text;
        
        const checkInCard = btnCheckIn.closest('.action-card');
        const checkOutCard = btnCheckOut.closest('.action-card');

        if (!schoolSelect.value) {
            btnCheckIn.disabled = true;
            btnCheckOut.disabled = true;
            if (checkInCard) checkInCard.style.display = 'block';
            if (checkOutCard) checkOutCard.style.display = 'block';
            return;
        }

        const openRecord = currentOpenRecord;

        if (openRecord) {
            document.querySelectorAll('input[name="shift"]').forEach(cb => cb.disabled = true);

            if (openRecord.schoolName === selectedSchoolName) {
                btnCheckIn.disabled = true;
                btnCheckOut.disabled = false;
                if (checkInCard) checkInCard.style.display = 'none';
                if (checkOutCard) checkOutCard.style.display = 'block';
            } else {
                btnCheckIn.disabled = true;
                btnCheckOut.disabled = true;
                if (checkInCard) checkInCard.style.display = 'none';
                if (checkOutCard) checkOutCard.style.display = 'block';
            }
        } else {
            document.querySelectorAll('input[name="shift"]').forEach(cb => cb.disabled = false);
            btnCheckIn.disabled = false;
            btnCheckOut.disabled = true;
            if (checkInCard) checkInCard.style.display = 'block';
            if (checkOutCard) checkOutCard.style.display = 'none';
        }
    }

    // Check In Action
    btnCheckIn.addEventListener('click', () => {
        const schoolId = schoolSelect.value;
        if (!schoolId) {
            showToast("Vui lòng chọn Cơ sở làm việc trước khi Check In", "error");
            return;
        }

        const selectedShifts = Array.from(document.querySelectorAll('input[name="shift"]:checked'));
        if (selectedShifts.length === 0) {
            showToast("Vui lòng chọn ít nhất một ca làm việc", "error");
            return;
        }

        if (!navigator.geolocation) {
            showToast("Trình duyệt của bạn không hỗ trợ GPS", "error");
            return;
        }

        showLoading(true);
        navigator.geolocation.getCurrentPosition(
            async (position) => {
                const lat = position.coords.latitude;
                const lon = position.coords.longitude;
                const accuracy = position.coords.accuracy;

                try {
                    const response = await fetch('/api/attendance/checkin', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            'Authorization': `Bearer ${token}`
                        },
                        body: JSON.stringify({
                            latitude: lat,
                            longitude: lon,
                            schoolId: parseInt(schoolId),
                            selectedShifts: selectedShifts.map(cb => cb.value),
                            deviceId: deviceId,
                            accuracy: accuracy
                        })
                    });

                    const textResult = await response.text();
                    let result;
                    try { result = JSON.parse(textResult); } catch { result = textResult; }

                    if (response.ok) {
                        if (result.status && result.status.includes('InvalidLocation')) {
                            showToast("Lưu nháp thành công, nhưng bạn đang ở sai vị trí công ty!", "error");
                        } else {
                            showToast("Check-in thành công!", "success");
                        }
                        document.querySelectorAll('input[name="shift"]').forEach(cb => cb.checked = false);
                        loadState();
                    } else {
                        let errorMessage = "Lỗi Check-in";
                        if (typeof result === 'string') {
                            errorMessage = result;
                        } else if (result.message) {
                            errorMessage = result.message;
                        } else if (result.title) {
                            errorMessage = result.title;
                        }
                        showToast(errorMessage, "error");
                        showLoading(false);
                    }
                } catch (error) {
                    showToast("Lỗi kết nối máy chủ", "error");
                    showLoading(false);
                }
            },
            (error) => {
                showLoading(false);
                showToast("Không thể lấy vị trí GPS", "error");
            },
            { enableHighAccuracy: true, timeout: 10000, maximumAge: 0 }
        );
    });

    // Check Out Action
    btnCheckOut.addEventListener('click', async () => {
        if (!currentOpenRecord) return;

        const now = new Date();
        const currentTime = now.getHours() * 100 + now.getMinutes();

        let isEarly = false;
        if (currentOpenRecord.selectedShifts) {
            try {
                const shifts = currentOpenRecord.selectedShifts.split(', ');
                const lastShift = shifts[shifts.length - 1];
                const endTimeStr = lastShift.split('-')[1].trim();
                const [endH, endM] = endTimeStr.split(':').map(Number);
                const endTimeValue = endH * 100 + endM;

                if (currentTime < endTimeValue) isEarly = true;
            } catch (e) { console.error(e); }
        } else if (now.getHours() < 17) {
            isEarly = true;
        }

        if (isEarly) {
            document.getElementById('earlyCheckoutModal').style.display = 'flex';
        } else {
            performCheckOut();
        }
    });

    document.getElementById('btnCancelEarlyCheckout').addEventListener('click', () => {
        document.getElementById('earlyCheckoutModal').style.display = 'none';
        document.getElementById('earlyLeaveReason').value = '';
    });

    document.getElementById('btnConfirmEarlyCheckout').addEventListener('click', () => {
        const reason = document.getElementById('earlyLeaveReason').value.trim();
        if (!reason) {
            showToast("Vui lòng nhập lý do về sớm", "error");
            return;
        }
        document.getElementById('earlyCheckoutModal').style.display = 'none';
        performCheckOut(reason);
        document.getElementById('earlyLeaveReason').value = '';
    });

    async function performCheckOut(reason = null) {
        const schoolId = schoolSelect.value;
        showLoading(true);
        try {
            const response = await fetch('/api/attendance/checkout', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify({ schoolId: parseInt(schoolId), reason: reason })
            });

            if (response.ok) {
                showToast("Check-out thành công!", "success");
                loadState();
            } else {
                let errorMessage = "Lỗi Check-out";
                const textResult = await response.text();
                let result;
                try { result = JSON.parse(textResult); } catch { result = textResult; }
                
                if (typeof result === 'string') {
                    errorMessage = result;
                } else if (result.message) {
                    errorMessage = result.message;
                } else if (result.title) {
                    errorMessage = result.title;
                }
                showToast(errorMessage, "error");
                showLoading(false);
            }
        } catch (error) {
            showToast("Lỗi kết nối máy chủ", "error");
            showLoading(false);
        }
    }

    // Fetch Schools
    async function loadSchools() {
        try {
            const response = await fetch('/api/attendance/schools', {
                headers: { 'Authorization': `Bearer ${token}` }
            });
            if (response.ok) {
                const schools = await response.json();
                schoolSelect.innerHTML = '<option value="">-- Chọn Cơ Sở --</option>';
                schools.forEach(s => {
                    const option = document.createElement('option');
                    option.value = s.id;
                    option.textContent = s.name;
                    schoolSelect.appendChild(option);
                });
            }
        } catch (error) {
            console.error(error);
        }
    }

    loadSchools();
    loadState();
});
