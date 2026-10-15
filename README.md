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

Phiên bản đích đã được đặt trong `ProjectVersion.txt`; trường revision của Editor cũ đã được bỏ để Unity đích ghi lại revision chính xác. **Chưa chạy import, biên dịch player hoặc Play Mode bằng Unity 6000.6.3f1 trong môi trường cloud này.** Xem [ghi chú nâng cấp](docs/unity-upgrade.md) để biết các thay đổi và phần cần xác minh.

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

Các kiểm tra biên dịch và chạy mã thực tế của `Config`, `Boot` và `UIScrollToSelection` với các adapter Unity tối thiểu. Chúng kiểm tra xử lý cấu hình, thứ tự coroutine, giữ dữ liệu khi từ chối APK cập nhật và các nhánh Input System. Chúng **không thay thế** Unity Test Runner, import package hay kiểm thử game trên thiết bị.

## Công cụ và đóng góp

- `Tools/Translations/translations split.py`: chạy từ `Tools/Translations`; đọc `translations.csv` và ghi đè `Data/locales/*/translation.conf`. Sửa CSV trước, không sửa trực tiếp các file dịch sinh ra. Dùng bản sao trong thư mục tạm khi chỉ kiểm tra công cụ.
- `Tools/YGO Classes`: mã nguồn native kế thừa từ YGOPro.
- `Tools/DumpShaders`: shader dùng khi đóng gói bundle.
- `Tools/QuickBMS`: công cụ xử lý dữ liệu item.
- `bot.conf` kế thừa từ [YGOMobile](https://github.com/fallenstardust/YGOMobile-cn-ko-en).

Tên trường JSON `MDPro3VersionUrl` vẫn được giữ để tương thích cấu hình, nhưng URL mặc định đã trỏ tới `Version.txt` của repository này. `Version.txt` và Player Settings cùng dùng phiên bản ứng dụng `1.2.5`.
