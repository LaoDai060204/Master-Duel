# Nâng cấp Master-Duel lên Unity 6000.6.3f1

## Trạng thái

Đích đã xác nhận qua tag `6000.6.3f1` trong Unity-Technologies/UnityCsReference (commit `ed99dc1ddc11ec2f31893417d1ad7c142977d402`). Checkout ban đầu ghim `6000.0.28f1`; README cũ nhắc `6000.0.10f1` và còn dấu merge conflict.

Đã đổi phiên bản đích, thông tin sản phẩm và sửa các lỗi có thể xác định từ mã nguồn. Chưa thể tuyên bố nâng cấp hoàn tất: môi trường cloud chưa có Unity Editor hoặc giấy phép được xác minh, và chưa chạy import, build hay Play Mode. Không dùng kết quả của các adapter .NET để suy ra game đã hoạt động trong Unity.

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
- `UIScrollToSelection` hỗ trợ Input System mới khi bật hủy cuộn, đồng thời kiểm tra EventSystem/content còn thiếu.
- Menu chỉ thông báo khi phiên bản cập nhật hợp lệ và mới hơn, tránh báo nhầm bản cũ hoặc phản hồi HTML là bản cập nhật.
- Công cụ Editor được đối chiếu với API trong mã tham chiếu Unity đích; xem diff để biết các thay đổi TreeView và EntityId.

`Tools/Tests/run.sh` chạy kiểm tra hành vi các bản sửa runtime bằng .NET 8. Mã nguồn gốc có encoding GB18030 được giữ nguyên; harness khởi động chuyển bản sao sang UTF-8 trong `obj` trước khi biên dịch. Không thay đổi file nguồn để phục vụ harness.

Kết quả: Config desktop **14/14**, Config Android **14/14**, Boot **3/3**; input New **50**, Legacy **7**, Both **7**, None **6** lượt kiểm tra đạt. Tổng **101 lượt** qua các biến thể. Các lỗi cấu hình/khởi động và thiếu EventSystem đã được tái hiện bằng cùng harness trên mã trước sửa. 18 file AssetBundle Browser được phân tích cú pháp, 35 phương thức override TreeView được đối chiếu với mã tham chiếu chính thức; chưa phải kết quả build Editor.

## Phần cần xác minh trong Unity

1. Cài đúng Editor, xác minh chữ ký/checksum phân phối và giấy phép. Trong cloud, truy cập `download.unity3d.com` và `packages.unity.com` đã hoạt động; yêu cầu metadata ở `services.api.unity.com` vẫn trả HTTP 403 lúc kiểm tra.
2. Mở dự án bằng 6000.6.3f1, kiểm tra Package Manager và xem diff sau API Updater. Manifest/lockfile hiện giữ các phiên bản package gốc; chưa xác nhận URP 17.0.3 và toàn bộ package tương thích Editor đích. Không nâng hàng loạt package khi chưa có chẩn đoán từ Editor.
3. Chạy import batch với đường dẫn Editor thực tế:

   ```sh
   /path/to/6000.6.3f1/Editor/Unity -batchmode -nographics -quit \
     -projectPath /workspace/Master-Duel -logFile /tmp/master-duel-import.log
   ```

4. Kiểm tra exit code, lỗi compile và log. Khi import đạt, xác minh công cụ AssetBundle Browser/Spine Editor, rồi thử `Assets/Boot.unity` với Addressables và tài nguyên đầy đủ.
5. Build player Windows hoặc Android với module tương ứng. Trên Android, thử cài mới gói đầy đủ, từ chối gói cập nhật khi chưa có bản nền, cập nhật trên bản đã cài và giữ nguyên dữ liệu.

Batch import trên chưa được thực thi. Chưa có kết quả Unity Test Runner hoặc build player; các test hiện có không xác minh thư viện native, đồ họa, mạng hay luồng duel.
