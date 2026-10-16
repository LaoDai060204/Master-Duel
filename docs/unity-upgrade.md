# Nâng cấp Master-Duel lên Unity 6000.6.3f1

## Trạng thái

Editor Linux **6000.6.3f1 (45d8eee7de74)** đã được cài tại `/workspace/.tools/unity/6000.6.3f1/Editor/Unity`. Gói chính thức 4.157.358.476 byte khớp MD5 Unity công bố `6b3fb34f445221e3e1b5e439d7e1d87e`; `Unity -version` và chạy lặp lại script cài đặt đều đạt.

Batch import đã được thử lại sau các bản sửa và dừng với **exit 198**: `No valid Unity Editor license found. Please activate your license.` Log hiện tại: `/tmp/master-duel-unity-import.log`. Chưa có import, Unity Test Runner, Play Mode hoặc player build thành công. Các kiểm tra bên dưới xác minh những phạm vi cụ thể; không xác nhận toàn bộ game đã chạy được.

## Cấu trúc và các bản sửa

Xem [cấu trúc mã](architecture.md). Mã sản phẩm giữ C# 9 phù hợp với Unity đích; .NET SDK 8 chỉ dùng cho công cụ kiểm tra độc lập. Giữ GUID, tên MonoBehaviour, namespace `MDPro3`, giấy phép và ghi nhận gốc.

- `Boot` điều khiển chuỗi iterator, dừng đúng bước khi đọc/giải nén/catalog/Items/scene thất bại; chỉ ghi dấu cài ZIP khi hoàn tất. Thay `WWW` bằng `UnityWebRequest` có timeout và disposal. Kiểm tra đường dẫn toàn bộ ZIP trước khi ghi.
- `Program` áp dụng kết quả từ parser dòng lệnh thuần C#, tránh đọc lại giá trị như một flag và tránh phụ thuộc thứ tự `-j`/`-d`.
- `Config` giữ giá trị chứa `->`, dùng mặc định cho dữ liệu số sai, không đưa thiết lập file trước sang file mới.
- `Settings` dùng mặc định từ constructor và bỏ quét tên thuộc tính bằng chuỗi. Đọc file hiện có không ghi mất trường mở rộng, cách định dạng hoặc giá trị dùng tên thuộc tính không phân biệt hoa/thường.
- `TextureLoader` giới hạn tải bằng semaphore, trả suất trong `finally`, hoàn tất các nhánh lỗi cache và quản lý texture theo đúng owner/generation. Kết quả đang tải cũ không phá cache mới sau `DeleteCache`.
- `MyCard` serialize JSON đúng, xử lý body lỗi HTTP không phải JSON, escape username trong URL và cache avatar theo hash URL. Giải phóng texture thừa, giữ cache bộ nhớ khi ghi đĩa thất bại.
- `OnlineDeck` giữ các slot cover riêng; chỉ đổi metadata sau xác nhận máy chủ. Lưu qua file tạm/thay thế và so sánh snapshot nội dung/identity trước khi commit kết quả đang chờ. Luồng upload/download/delete giữ bản chỉnh sửa mới và phản hồi đến sai thứ tự không ghi đè dữ liệu mới. Các deck mới xuất hiện giữa lượt đồng bộ được xử lý ở lượt tiếp theo.
- `MessageManager` giữ FIFO trước khi prefab sẵn sàng và khi vượt giới hạn hiển thị; giải phóng các handle lỗi, callback cũ, khởi tạo lại và hủy manager.
- API TreeView generic/EntityId của các công cụ Editor đã chuyển sang API đích. Bổ sung tham chiếu `UnityEngine.UI` còn thiếu trong ba `asmdef` Spine.

Thông tin sản phẩm: **Master-Duel / LaoDai060204**, application ID `com.laodai060204.masterduel`. Kênh cập nhật trỏ repository của fork. Application ID mới tạo danh tính và có thể dùng vị trí lưu dữ liệu khác trên thiết bị.

## Package

Manifest/lock đã được đồng bộ từ metadata thực trong gói Editor đã kiểm checksum, không phải kết quả Unity resolver:

| Package | Phiên bản đích |
| --- | --- |
| URP/Core/ShaderGraph/Universal Config | 17.6.0 |
| UGUI | 2.6.0 |
| Timeline | 6.6.0 |
| Test Framework / NUnit | 1.8.0 / 2.1.0 |
| Multiplayer Center | 2.0.1 |

Đã bỏ module `com.unity.modules.vr` không còn được cung cấp, entry builtin `light-transport` cũ không còn reachable và scoped registry YooAsset không dùng. Bổ sung dependency cần thiết từ metadata Editor; giữ nguyên cả ba revision/hash Git. Các pin registry có thay đổi API lớn như Cinemachine 2.10.3 được giữ để chờ chuyển đổi code/scene riêng.

`python3 Tools/Unity/check-packages.py` đạt với **51 phụ thuộc gốc / 70 mục khóa**, không có lỗi hoặc cảnh báo chưa giải quyết. Sáu thông tin về pin registry được giữ được in rõ. Kiểm tra này xác minh graph metadata và phiên bản tối thiểu; chưa tải/resolve toàn bộ package.

## Kiểm tra đã chạy

Từ repository root:

```sh
DOTNET_CLI_HOME=/tmp/master-duel-dotnet-home DOTNET=/workspace/.tools/dotnet/dotnet bash Tools/Tests/run.sh
```

| Bộ kiểm tra | Kết quả/phạm vi |
| --- | --- |
| Config | 15 desktop + 15 Android |
| Settings | 14 tình huống |
| Startup | 13 tình huống; ZIP dùng SharpZipLib thật |
| Command line / archive paths | 10 tình huống |
| Texture | 58 kiểm tra, gồm owner/waiter đang tải và cache reset |
| MyCard | 31 tình huống JSON/HTTP/avatar |
| OnlineDeck | 39 tình huống / 148 assertion |
| Message | 12 tình huống khởi tạo, FIFO và handle |
| Scroll | 50/7/7/6 theo New/Legacy/Both/None |
| SourceAudit | 3.008 file C# qua Editor/Linux, Windows, Android; 0 lỗi cú pháp |

Các harness liên kết source thật và thay adapter tại ranh giới Unity/HTTP/UI. Một số dùng Newtonsoft.Json của SDK; Startup dùng SharpZipLib đi kèm repository. Source GB18030 được staging thành UTF-8 trong `obj` khi cần, giữ nguyên file gốc. Chúng chưa kiểm tra renderer, server thật hoặc hành vi transaction với chương trình ngoài cùng sửa file giữa lần kiểm tra và lần ghi.

`Tools/Unity/compile-vendor.py` tuân thủ ranh giới `asmdef`, dùng compiler/DLL thật đi kèm Editor và chỉ cấp tham chiếu được khai báo. Kết quả: **Editor 7 assembly / 193 file C#**, **Standalone 4 / 122**, **Android 4 / 122**, tất cả đạt. Cấu hình runtime không cấp DLL UnityEditor. Đây là kiểm tra API độc lập, chưa phải player build hoặc biên dịch toàn bộ Assembly-CSharp; warning vẫn còn.

## Phần còn cần xác minh

1. Kích hoạt giấy phép Unity hợp lệ trên máy chạy Editor. Các miền metadata/download Editor đã truy cập được; tải archive package từ `download.packages.unity.com` trong cloud vẫn trả 403 ở lần thử gần nhất, kể cả khi đã chạy với quyền ngoài sandbox.
2. Chạy lại Package Manager/import, kiểm tra thay đổi do resolver và API Updater. Các package registry được giữ cần kiểm chứng trên Editor có license. Không sửa checksum hoặc tắt xác minh TLS để vượt lỗi tải.
3. Khôi phục tài nguyên ngoài repository: bundle nền tảng, Picture/Art, Picture/Closeup, Picture/DIY, Sound và cấu hình/catalog Addressables. Lõi `.so` đi kèm là Android ARM64; Linux Editor x86_64 cần thư viện phù hợp để chạy duel.
4. Chạy import bằng lệnh:

   ```sh
   /workspace/.tools/unity/6000.6.3f1/Editor/Unity -batchmode -nographics -quit \
     -projectPath /workspace/Master-Duel -job-worker-count 4 -accept-apiupdate \
     -logFile /tmp/master-duel-unity-import.log
   ```

5. Sau khi import đạt, kiểm thử scene Boot, công cụ Spine/AssetBundle, đăng nhập, đồng bộ deck với server thử nghiệm, render/cache ảnh và build Windows/Android với module nền tảng tương ứng.
