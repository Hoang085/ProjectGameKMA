# SystemManager Setup Instructions

## Giải pháp cho vấn đề Manager bị destroy khi chuyển scene

### Vấn đề
Khi load từ GameScene sang ExamScene, chỉ có GameManager được giữ lại (do có DontDestroyOnLoad), còn tất cả các managers khác như:
- TaskManager
- TeachersSpawnManager  
- TimeSaveManager
- NotesServiceManager
- PlayerSaveManager
- AttendanceManager
- NavigationLineManager
- IconNotificationManager

...đều bị destroy, gây ra lỗi null reference.

### Giải pháp
Tạo PersistentSystemManager để quản lý toàn bộ hệ thống managers.

## Hướng dẫn Setup

### 1. Tạo SystemManager GameObject trong GameScene

```
1. Trong GameScene, tạo Empty GameObject
2. Đặt tên: "SystemManager" 
3. Attach script "PersistentSystemManager"
4. Đảm bảo tất cả managers (TaskManager, IconNotificationManager...) là children của SystemManager
```

### 2. Hierarchy nên như sau:

```
SystemManager
├─ TaskManager
├─ TeachersSpawnManager
├─ TimeSaveManager
├─ NotesServiceManager
├─ PlayerSaveManager
├─ AttendanceManager
├─ NavigationLineManager  
├─ IconNotificationManager
└─ [Other managers...]
```

### 3. Tự động phát hiện và quản lý
PersistentSystemManager sẽ:
- Tự động tìm tất cả managers trong scene
- Gọi DontDestroyOnLoad cho từng manager
- Reparent chúng thành children của SystemManager
- Validate sau mỗi lần chuyển scene

### 4. Test và Debug

#### Context Menu Commands:
- **Force Refresh All Managers**: Tìm lại tất cả managers
- **Show All Managed Components**: Hiển thị danh sách tất cả managers đã đăng ký
- **Test DontDestroyOnLoad**: Kiểm tra xem các managers có trong DontDestroyOnLoad scene không
- **Ensure All Managers Enabled**: Đảm bảo tất cả managers được enable

#### Debug Console:
```
[PersistentSystemManager] ✓ Registered TaskManager: TaskManager
[PersistentSystemManager] ✓ Registered IconNotificationManager: IconNotificationManager
[PersistentSystemManager] ↳ Reparented TaskManager to PersistentSystemManager
```

## Tích hợp với hệ thống hiện tại

### GameManager Integration
- GameManager vẫn hoạt động bình thường với DontDestroyOnLoad riêng
- PersistentSystemManager bổ sung để quản lý các managers khác
- Không xung đột với GameManager

### ExamUIManager Integration
- Đã được sửa để trở về GameScene đúng cách
- Sử dụng PlayerPrefs flag để trigger state restoration
- GameManager sẽ detect và khôi phục state

### State Management
- TeacherAction đã được sửa để lưu state trước khi vào thi
- GameStateManager xử lý khôi phục state sau khi thi xong
- Unblock tất cả systems sau khi khôi phục

## Expected Behavior

### Trước khi có SystemManager:
```
GameScene -> ExamScene: Chỉ GameManager còn lại, các managers khác destroy
ExamScene -> GameScene: null reference errors, notifications không hoạt động
```

### Sau khi có SystemManager:
```
GameScene -> ExamScene: Tất cả managers được giữ lại trong DontDestroyOnLoad
ExamScene -> GameScene: Khôi phục hoàn hảo, tất cả systems hoạt động bình thường
```

## Troubleshooting

### Nếu managers vẫn bị destroy:
1. Kiểm tra hierarchy: managers phải là children của SystemManager
2. Chạy "Test DontDestroyOnLoad" context menu
3. Kiểm tra log để đảm bảo tất cả managers được register

### Nếu có null reference sau khi return:
1. Chạy "Force Refresh All Managers"
2. Kiểm tra GameManager có call RefreshIconNotificationManager không
3. Verify GameStateManager.UnblockGameSystems được gọi

### Performance Notes:
- SystemManager chỉ chạy validation 1 lần sau Start()
- Không có overhead trong runtime
- Chỉ activate khi chuyển scene