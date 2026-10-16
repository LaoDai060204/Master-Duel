# Cấu trúc mã cho Unity 6000.6.3f1

Mã ứng dụng dùng các API của Unity đích và cú pháp C# 9. SDK .NET 8 phục vụ các công cụ kiểm tra độc lập; không đổi runtime của Unity sang .NET 8. Giữ tên MonoBehaviour, namespace và GUID hiện có để bảo toàn tham chiếu scene/prefab.

| Thành phần | Trách nhiệm |
| --- | --- |
| `Boot`, `Program` | Điều phối khởi động, kiểm tra kết quả Addressables, báo đúng bước thất bại và chỉ chuyển scene khi dữ liệu sẵn sàng. |
| `Utility/CommandLineOptions` | Phân tích tham số thuần C#, không ghi cấu hình hoặc điều khiển UI. `Program` áp dụng kết quả sau khi phân tích xong. |
| `Utility/ArchivePath` | Chuẩn hóa đường dẫn entry ZIP và xác nhận nằm trong thư mục giải nén trước khi ghi. |
| `Helper/Settings` | Constructor cung cấp mặc định cho schema; JSON loader giữ giá trị hợp lệ, trường mở rộng và nội dung file hiện có. |
| `UI/Function/TextureLoader`, `TextureData` | Quản lý cache, quyền sở hữu texture, giới hạn tải bằng semaphore và vô hiệu hóa yêu cầu cũ khi xóa cache. |
| `Net/MyCard` | Serialize payload JSON, xử lý lỗi HTTP và cache avatar theo URL. |
| `Net/OnlineDeck` | Chuẩn bị bản dữ liệu gửi; chỉ cập nhật sau xác nhận máy chủ. Lưu file qua file tạm và thay thế; kiểm tra dữ liệu cục bộ chưa đổi trước khi hoàn tất một yêu cầu cũ. |
| `Managers/MessageManager` | Xếp hàng thông báo khi prefab chưa sẵn sàng, phát sau khi tải thành công và quản lý vòng đời handle. |
| `Tools/Tests` | Kiểm tra hành vi bằng source thật và adapter tại ranh giới Unity/HTTP/UI, có trường hợp lỗi và yêu cầu đang chờ. |
| `Tools/Unity` | Kiểm tra metadata package và biên dịch các assembly vendor theo tham chiếu `asmdef` thực tế. |

Các bản sửa giữ coroutine tại ranh giới lifecycle/scene của Unity và dùng `Task`/`await` trong các hàm I/O hiện có. Tài nguyên request, stream, handle và texture có đường giải phóng rõ ràng khi thành công, thất bại hoặc thoát. Không cần đổi mọi coroutine thành `async void`; các thao tác có kết quả được quan sát tại bên gọi.

Package builtin được đối chiếu với Editor đã cài. Các package registry có thay đổi API lớn, như Cinemachine, vẫn được ghim khi chưa hoàn tất chuyển đổi code và scene. Thay đổi metadata lockfile có nguồn từ manifest đi kèm Editor; kết quả đó chưa thay thế một lần Unity Package Manager resolve thành công.

## Kiểm tra

```sh
DOTNET=/workspace/.tools/dotnet/dotnet bash Tools/Tests/run.sh
python3 Tools/Unity/check-packages.py
python3 Tools/Unity/compile-vendor.py --platform editor
python3 Tools/Unity/compile-vendor.py --platform standalone
python3 Tools/Unity/compile-vendor.py --platform android
```

`SourceAudit` phân tích toàn bộ C# trong `Assets` với ba bộ cờ Editor/Linux, Windows và Android. Biên dịch vendor dùng DLL và compiler đi kèm Editor, theo ranh giới assembly. Các kiểm tra độc lập không chạy renderer, native duel core hoặc toàn bộ dependency graph của game; import/Play Mode/player build cần Editor có giấy phép và tài nguyên game đầy đủ.
