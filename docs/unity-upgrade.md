# Nâng cấp Master-Duel lên Unity 6000.6.3f1

## Trạng thái

Đích đã xác nhận qua tag `6000.6.3f1` trong Unity-Technologies/UnityCsReference (commit `ed99dc1ddc11ec2f31893417d1ad7c142977d402`). Checkout ban đầu ghim `6000.0.28f1`; README cũ nhắc `6000.0.10f1` và còn dấu merge conflict.

Đã cài Unity Editor Linux **6000.6.3f1 (45d8eee7de74)** tại `/workspace/.tools/unity/6000.6.3f1/Editor/Unity`. Revision được xác nhận qua API phát hành Unity; archive 4.157.358.476 byte khớp checksum MD5 Unity công bố `6b3fb34f445221e3e1b5e439d7e1d87e`. `Unity -version` trả đúng phiên bản; script cài đặt chạy lại thành công và tái sử dụng bản có sẵn.

Batch import đã chạy trong sandbox và thử lại với quyền cần thiết. Cả hai dừng với **exit 198**: `No valid Unity Editor license found. Please activate your license.` Log: `/tmp/master-duel-unity-import.log`. Không thể xác nhận import/build/Play Mode hoàn tất khi giấy phép chưa được kích hoạt; đây là blocker môi trường, không phải lỗi C# đã quan sát từ Unity.

## Cá nhân hóa

- Product: `Master-Duel`; company: `LaoDai060204`.
- Application ID Android/Standalone/iOS: `com.laodai060204.masterduel`; Product GUID mới cho fork.
- Menu lấy tên từ `Application.productName`.
- Kiểm tra phiên bản ứng dụng đọc repository `LaoDai060204/Master-Duel`, giữ tên trường cấu hình cũ để tương thích.
- Giữ nguyên giấy phép, ghi nhận dự án gốc và namespace `MDPro3`.

Application ID mới tạo danh tính ứng dụng riêng. Bản cài cũ có thể dùng vị trí lưu dữ liệu khác; cần sao lưu dữ liệu trước khi thử cập nhật trên thiết bị.

## Thay đổi và kiểm tra

- Bỏ import `UnityEditor` không dùng khỏi mã runtime của menu, tránh lỗi build player.
- `Boot` chỉ để một scheduler điều khiển mỗi coroutine giải nén và chờ coroutine con hoàn tất.
- Từ chối APK cập nhật lần đầu không xóa `Data` hoặc ném lỗi thư mục không rỗng.
- `Config.Initialize` không đưa thiết lập của file cũ vào file mới. Giá trị số sai/tràn và UI scale không hợp lệ dùng mặc định.
- Giá trị cấu hình chứa `->` được tách tại dấu phân cách đầu tiên để không mất sau khi lưu/đọc lại.
- `Settings.json` rỗng, JSON null/sai kiểu hoặc trường null dùng mặc định an toàn; giữ các giá trị tùy chỉnh hợp lệ và mảng rỗng chủ ý. Khởi tạo lần đầu tạo thư mục `Data` khi cần.
- `UIScrollToSelection` hỗ trợ Input System mới khi bật hủy cuộn, đồng thời kiểm tra EventSystem/content còn thiếu.
- Menu chỉ thông báo khi phiên bản cập nhật hợp lệ và mới hơn, tránh báo nhầm bản cũ hoặc phản hồi HTML là bản cập nhật.
- Công cụ Editor được đối chiếu với API trong mã tham chiếu Unity đích; xem diff để biết các thay đổi TreeView và EntityId.

`Tools/Tests/run.sh` chạy kiểm tra hành vi các bản sửa runtime bằng .NET 8. Mã nguồn gốc có encoding GB18030 được giữ nguyên; harness khởi động chuyển bản sao sang UTF-8 trong `obj` trước khi biên dịch. Không thay đổi file nguồn để phục vụ harness.

Kết quả: Config desktop **15/15**, Config Android **15/15**, Settings **11/11**, Boot **3/3**; input New **50**, Legacy **7**, Both **7**, None **6** lượt kiểm tra đạt. Tổng **114 lượt** qua các biến thể. Settings trước sửa đạt 3/11; kiểm tra dấu phân cách cấu hình cũng thất bại trước sửa. Các adapter không chứng minh toàn bộ game chạy được trong Unity.

`python3 Tools/Unity/compile-vendor.py` dùng compiler, DLL .NET Standard/Unity và cache DLL template đi kèm Editor đã cài, không có Unity API giả. Biên dịch **4 assembly/169 file C#** thành công: AssetBundle Browser (18), spine-csharp (47), spine-unity (59), spine-unity-editor (45). Mã nguồn được staging sang thư mục tạm, giữ nguyên source trong checkout. Đây là kiểm tra API C# độc lập; chưa bao gồm toàn bộ Assembly-CSharp, Unity asset import, package resolution hoặc player build. Warning chưa được xử lý hết.

## Phần cần xác minh trong Unity

1. Kích hoạt giấy phép Unity hợp lệ trên môi trường chạy Editor. Không đưa thông tin tài khoản hoặc license vào source. Các miền metadata/download/registry hiện đã truy cập được; lỗi mạng 403 trước đó đã được gỡ. Cài/tái sử dụng Editor bằng `bash Tools/Unity/install-editor.sh`.
2. Mở dự án bằng 6000.6.3f1, kiểm tra Package Manager và xem diff sau API Updater. Manifest/lockfile hiện giữ các phiên bản package gốc; chưa xác nhận toàn bộ package tương thích Editor đích. Editor mới đi kèm URP/Core/ShaderGraph **17.6.0** và UGUI **2.6.0**, còn manifest cũ yêu cầu URP 17.0.3/UGUI 2.0.0. Cần để Package Manager resolve và kiểm tra diff; không tự ghi lockfile khi chưa chạy resolver.
3. Chạy import batch với đường dẫn Editor thực tế:

   ```sh
   /workspace/.tools/unity/6000.6.3f1/Editor/Unity -batchmode -nographics -quit \
     -projectPath /workspace/Master-Duel -job-worker-count 4 -accept-apiupdate \
     -logFile /tmp/master-duel-unity-import.log
   ```

4. Kiểm tra exit code, lỗi compile và log. Khi import đạt, xác minh công cụ AssetBundle Browser/Spine Editor, rồi thử `Assets/Boot.unity` với Addressables và tài nguyên đầy đủ.
5. Build player Windows hoặc Android với module tương ứng. Trên Android, thử cài mới gói đầy đủ, từ chối gói cập nhật khi chưa có bản nền, cập nhật trên bản đã cài và giữ nguyên dữ liệu.

Batch import trên đã thực thi nhưng chưa qua bước kiểm tra giấy phép. Chưa có kết quả Unity Test Runner hoặc build player; các test hiện có không xác minh thư viện native, đồ họa, mạng hay luồng duel.
