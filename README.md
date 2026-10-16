# Master-Duel

Bản tùy biến của **LaoDai060204**, phát triển từ MDPro3: YGOPro trong Unity với tài nguyên Master Duel.

- Repository: https://github.com/LaoDai060204/Master-Duel
- Mã nguồn gốc: https://code.moenext.com/sherry_chaos/MDPro3
- Unity đích: **6000.6.3f1**.
- Tên sản phẩm: **Master-Duel**; company: **LaoDai060204**.
- Application ID: `com.laodai060204.masterduel`.

Giữ nguyên [giấy phép GPL-3.0](LICENSE), ghi nhận MDPro3 và các giấy phép/thông báo của thành phần bên thứ ba. Namespace `MDPro3` được giữ để bảo toàn tham chiếu của mã nguồn và tài nguyên Unity.

## Mở dự án

1. Cài Unity Editor **6000.6.3f1** bằng Unity Hub và kích hoạt giấy phép hợp lệ. Phiên bản này được xác nhận trong [mã tham chiếu chính thức của Unity](https://github.com/Unity-Technologies/UnityCsReference/tree/6000.6.3f1).
2. Mở thư mục chứa `Assets`, `Packages` và `ProjectSettings`; chờ Package Manager và quá trình import hoàn tất. Các phụ thuộc được lưu trong `Packages/manifest.json` và `Packages/packages-lock.json`.
3. Kiểm tra Console, rồi mở scene `Assets/Boot.unity`.

Phiên bản đích đã được đặt trong `ProjectVersion.txt`, kèm revision chính thức `45d8eee7de74` từ API phát hành Unity. **Editor 6000.6.3f1 đã được cài và kiểm tra checksum. Batch import đã thử nhưng dừng với mã 198 vì chưa có giấy phép Unity hợp lệ; chưa có build player hoặc Play Mode thành công.** Xem [ghi chú nâng cấp](docs/unity-upgrade.md) để biết các thay đổi và phần cần xác minh.

## Công cụ nâng cấp trong cloud

```sh
bash Tools/Unity/install-editor.sh
python3 Tools/Unity/check-packages.py
python3 Tools/Unity/compile-vendor.py --platform editor
```

Script cài Editor Linux dùng gói chính thức, kiểm tra checksum Unity công bố và tái sử dụng bản đã cài. Script biên dịch dùng compiler và DLL thật của Editor, theo tham chiếu `asmdef`: 7 assembly/193 file C# đạt cho Editor; 4 assembly/122 file đạt cho mỗi cấu hình Standalone và Android. Kiểm tra metadata đạt với 51 phụ thuộc gốc/70 mục khóa; 3.008 file C# được phân tích cú pháp qua ba cấu hình. Đây là kiểm tra biên dịch độc lập, không thay thế import/Play Mode hoặc xác nhận toàn bộ package của game đã được resolve.

## Tài nguyên cần bổ sung

Repository chưa có các thư mục dữ liệu cần cho game đầy đủ:

| Thư mục | Nguồn được dự án gốc hướng dẫn |
| --- | --- |
| `Android` | Bản APK MDPro3 |
| `StandaloneWindows64` | Bản phát hành MDPro3 Windows |
| `Picture/Art` | https://code.mycard.moe/mycard/hd-arts |
| `Picture/Closeup` | https://code.mycard.moe/mycard/ygopro2-closeup |
| `Picture/DIY`, `Sound` | Bản phát hành MDPro3 |

Cấu hình `Assets/AddressableAssetsData` cũng chưa có trong checkout; cần khôi phục/cấu hình catalog và các key như `Items`, `Main` trước khi xác minh luồng khởi động. Các `.so` của lõi duel đi kèm là Android ARM64; để chơi trong Linux Editor x86_64 cần thư viện native phù hợp. Windows và Android là các nền tảng có thư viện lõi đi kèm.

## Kiểm tra hồi quy độc lập

Cần **.NET SDK 8** và **Python 3**. Từ thư mục gốc repository:

```sh
bash Tools/Tests/run.sh
```

Trong cloud hiện tại, SDK nằm ở `/workspace/.tools/dotnet`; có thể chạy `DOTNET=/workspace/.tools/dotnet/dotnet bash Tools/Tests/run.sh`.

Các kiểm tra chạy mã thực tế của cấu hình, khởi động, tham số dòng lệnh, tải ảnh, MyCard, đồng bộ deck, hàng đợi thông báo và cuộn UI với adapter tại ranh giới Unity/HTTP. Chúng bao gồm phản hồi thất bại, kết quả đến sai thứ tự, chỉnh sửa khi đang chờ mạng và tài nguyên đang tải khi bị hủy. Xem [cấu trúc mã](docs/architecture.md) và [kết quả kiểm tra](docs/unity-upgrade.md). Chúng **không thay thế** Unity Test Runner, import package hay kiểm thử game trên thiết bị.

## Công cụ và đóng góp

- `Tools/Translations/translations split.py`: chạy từ `Tools/Translations`; đọc `translations.csv` và ghi đè `Data/locales/*/translation.conf`. Sửa CSV trước, không sửa trực tiếp các file dịch sinh ra. Dùng bản sao trong thư mục tạm khi chỉ kiểm tra công cụ.
- `Tools/YGO Classes`: mã nguồn native kế thừa từ YGOPro.
- `Tools/DumpShaders`: shader dùng khi đóng gói bundle.
- `Tools/QuickBMS`: công cụ xử lý dữ liệu item.
- `bot.conf` kế thừa từ [YGOMobile](https://github.com/fallenstardust/YGOMobile-cn-ko-en).

Tên trường JSON `MDPro3VersionUrl` vẫn được giữ để tương thích cấu hình, nhưng URL mặc định đã trỏ tới `Version.txt` của repository này. `Version.txt` và Player Settings cùng dùng phiên bản ứng dụng `1.2.5`.
