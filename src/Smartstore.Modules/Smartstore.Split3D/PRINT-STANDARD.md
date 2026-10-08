# Thông số in chuẩn — bảng tên 3D (TT Minimal)

Thông số đo từ 4 file in mẫu đã tối ưu của studio (08/10/2026). Đây là **mặc định** của trình thiết kế bảng tên
và của file in xuất cho đơn hàng. Khi đổi thông số, sửa ở đây trước rồi sửa code theo mục [Trong code](#trong-code).

## File mẫu

| File | Vai trò | Kích thước (mm) |
|---|---|---|
| `Difference_19.stl` | Khung "Tài" (chữ ngắn) | 54,43 × 38,86 × 4,00 |
| `Object_3.stl` | Nội dung "Tài" | 39,27 × 23,70 × 4,00 |
| `Difference_31.stl` | Khung "QuangTuấn" (chữ dài) | 145,91 × 52,15 × 4,00 |
| `Object_16.stl` | Nội dung "QuangTuấn" | 130,76 × 37,00 × 4,00 |

Xuất từ SketchUp, đơn vị mm. Kiểu: **ôm theo chữ, chữ ghép (inlay), lỗ móc bên trái**.

## Thông số chuẩn

| Thông số | Giá trị | Ghi chú |
|---|---|---|
| Phông | **Pacifico** | Khớp nhất trong các phông của shop (tỷ lệ Q/T 1,388 so với mẫu 1,378; độ rộng "QuangTuấn"/T 5,555 so với 5,517) |
| Cỡ chữ | **Chữ T hoa cao 23,7 mm** | Giống hệt ở cả hai mẫu (khối chữ "T" cùng 24,14 × 23,70 mm, cùng thể tích 684 mm³) → **cỡ chữ cố định, chữ dài thì khung dài ra** |
| Hình khung | Ôm theo chữ | Chữ nới đều ra một khoảng, góc tròn |
| Lề quanh chữ | **7,5 mm** | Đo 7,49–7,66 mm (dưới / trên); bên phải 6,87 ở đầu nét nhọn |
| Độ dày khung | **4,0 mm** | |
| Hốc trong khung | sâu **2,0 mm** = **50 %** độ dày | Đáy hốc dày 2,0 mm, không đục xuyên (lòng chữ o, a, d… vẫn dính đáy) |
| Mảnh nội dung | dày **4,0 mm** | 2 mm nằm trong hốc + **nhô 2,0 mm** khỏi mặt khung |
| Khe lắp | **0 mm** | Hốc trùng khít hình chữ (diện tích hốc = diện tích mảnh: 183,23 / 171,00 / 549,90 mm² …) |
| Lỗ móc | **Ø 4,0 mm**, bên trái | Tâm cách điểm trái nhất của chữ **≈ 3,9 mm** (mép lỗ cách chữ 1,9 mm), ngang tầm điểm đó |
| Thành quanh lỗ | **≈ 2,4 mm** ra mép ngoài | Khung phình ra một chút quanh lỗ (lề trái 8,3 mm thay vì 7,5) |
| Màu | Khung một màu, nội dung một màu | In riêng từng màu rồi ấn nội dung vào hốc |

### Tỷ lệ suy ra (dùng khi phóng to / thu nhỏ)

| Tỷ lệ | Giá trị |
|---|---|
| Lề / chiều cao chữ T | 7,5 / 23,7 ≈ **0,32** |
| Độ nổi / chiều cao chữ T | 2 / 23,7 ≈ **0,084** |
| Độ sâu hốc / độ dày khung | **50 %** |
| Chiều cao khung (không dấu, không nét xuống) | chữ T + 2 × lề ≈ 38,7 mm |
| Chiều dài khung | độ rộng chữ + lề phải 7,5 + (lề trái có lỗ ≈ 8,3) |

## Quy tắc trên trang sản phẩm

- **Mặc định**: loại *Ôm theo chữ (chuẩn)*, phông Pacifico, chữ T cao 23,7 mm, lề 7,5, dày 4, chữ nổi 2, lỗ trái.
- **Khung tự dài theo chữ**: khách gõ tên → thanh *Chiều dài* (và giá) tự nhảy theo chiều dài thật của khung.
  Ví dụ: "Tài" ≈ 5,5 cm, "QuangTuấn" ≈ 14,7 cm.
- **Kéo thanh Chiều dài** = phóng to / thu nhỏ cả mẫu theo hệ số `k` (tỷ lệ so với cỡ chuẩn, 0,35 – 3):
  - chữ T cao `23,7 × k` mm, lề `7,5 × k` (giới hạn 3 – 20 mm);
  - độ dày `4 × √k` (làm tròn 0,5; giới hạn 2,5 – 8 mm), độ nổi `2 × √k` (làm tròn 0,2; giới hạn 1 – 4 mm):
    bảng to gấp đôi không cần dày gấp đôi;
  - lỗ móc luôn Ø 4 mm (vừa khoen móc khoá).
- Tên dài vượt chiều dài tối đa đang bán (cài đặt studio, hiện 30 cm) → tự thu nhỏ cho vừa.
- Bảng *Đặt nhiều*: mỗi dòng tự tính chiều dài (giá) theo chữ của dòng đó.
- Các loại khác dùng chung độ dày 4 / độ nổi 2 / lề 7,5 khi hợp: để bàn, treo cửa, thẻ hành lý (lề 6);
  tag cài áo giữ mỏng (dày 3, nổi 1,4, lề 5). Bảng lớp và mã QR: khung dày 4 mm, QR chữ / mã nổi 2 mm.

## Quy tắc file in (trang đơn hàng admin)

- **Khung** + **Nội dung** (mỗi màu một file). Mã QR: một file **nguyên khối**.
- Mặc định: khe lắp **0 mm**, hốc sâu **50 %** độ dày khung, định dạng 3MF. Đổi được trên khối *File in 3D*
  (nút *Lưu làm mặc định* → `PrintFileSettings`).
- Mảnh nội dung mỏng nhất 0,6 mm.

## Trong code

| Thông số | Ở đâu |
|---|---|
| Phông, chữ T 23,7, lề 7,5, dày 4, nổi 2 | `STANDARD` trong `wwwroot/studio/studio-designs.js` (loại `keychain` = *Ôm theo chữ (chuẩn)*) |
| Co giãn theo tỷ lệ | `scaled(d)` và `sizeTo` trong `studio-designs.js`; thanh Chiều dài: `syncLength`, `scaleToLength`, `sizedRow` trong `studio.js` |
| Khung dài theo chữ | `capMm`, `contentBlock`, `naturalSize` trong `studio-nameplate.js` |
| Lỗ móc trong dải lề | `beside`, `HOLE_GAP = 1.9`, `HOLE_R = 2` trong `studio-nameplate.js` |
| Viền ôm chữ trơn đều | nhánh `shape === 'outline'` trong `masks` (trường khoảng cách `edt`) |
| Khe lắp, độ sâu hốc | `EXPORT` trong `studio-nameplate.js`; mặc định admin: `Configuration/PrintFileSettings.cs` |

## Đo lại từ file STL mới

Mở STL bằng script Python đọc STL nhị phân (đọc tam giác, không cần thư viện) và lấy:

1. **Khung bao** của mỗi file (x, y, z) → kích thước, độ dày.
2. **Các mặt nằm ngang** theo độ cao z: mặt hướng lên ở z = 2 là đáy hốc, ở z = 4 là mặt khung.
3. **Vòng biên** của các mặt đó: so diện tích từng vòng của hốc với mảnh nội dung → khe lắp
   (bằng nhau = khe 0); vòng nhỏ tròn ở mặt đáy → lỗ (diện tích 12,42 mm² = Ø 4).
4. **Khối liên thông** của file nội dung → kích thước từng chữ (so chữ "T" giữa các mẫu để biết cỡ chữ cố định hay co giãn).
5. So tỷ lệ rộng / cao của chữ với các phông của shop (đo trên canvas trình duyệt) để xác định phông.
