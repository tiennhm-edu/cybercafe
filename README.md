# CyberCafe ☕ — Dự án xương sống khóa .NET Full-stack

CyberCafe là ứng dụng **đặt món quán cà phê**: khách xem thực đơn, chọn size, thêm vào giỏ, đặt hàng; nhân viên pha chế (barista) nhận đơn mới; quản trị viên quản lý thực đơn.

Đây là **một codebase duy nhất** được phát triển dần qua các buổi 23 → 55 của khóa .NET Full-stack (giảng viên TienNHM). Mỗi mốc quan trọng được đánh dấu bằng một **git tag** — bạn nghỉ buổi nào chỉ cần checkout đúng tag là có code của buổi đó.

> Domain (Product/Coffee/Tea/Cake, Order/OrderItem, Discount, Payment, Customer/Employee) được port từ bài OOP `day-04/CoffeeShopManagement` — các bạn sẽ gặp lại đúng những class đã viết ở phần OOP.

---

## Dùng tag để theo kịp buổi học

```bash
# 1. Clone repo (1 lần)
git clone <url-repo> cybercafe
cd cybercafe

# 2. Xem danh sách mốc + mô tả
git tag -l -n1

# 3. Xem code đúng như cuối buổi 28 (chế độ "detached HEAD" — chỉ để xem/chạy)
git checkout b28-cart-state

# 4. Muốn code tiếp từ mốc đó → tạo nhánh riêng của bạn
git switch -c my-work b28-cart-state

# 5. So sánh 2 mốc: buổi này thêm/sửa gì?
git diff b23-blazor-start b28-cart-state --stat

# 6. Quay về bản mới nhất
git switch main
```

> ⚠️ Đang có thay đổi chưa commit thì `git checkout <tag>` sẽ báo lỗi. Hãy `git stash` hoặc commit vào nhánh riêng trước.

---

## Cách chạy

Yêu cầu: **.NET SDK 10** (repo pin `10.0.100` trong `global.json`, cho phép roll-forward lên bản 10.0.x mới hơn).

```bash
dotnet --version              # 10.0.x
dotnet build                  # build cả solution CyberCafe.slnx
dotnet test                   # chạy unit test
dotnet run --project src/CyberCafe.Web
```

Mở địa chỉ in ra trên console (mặc định `https://localhost:7xxx` / `http://localhost:5xxx`).

Gợi ý demo (tag `b28-cart-state`): **Thực đơn** → chọn size L, số lượng 2 → *Thêm vào giỏ* → badge giỏ hàng trên cùng cập nhật → **Giỏ hàng** → nhập mã `GIAM20K` hoặc `MEMBER10` → **Đặt hàng** → nhập SĐT sai để xem validate → đặt thành công → trang xác nhận đơn `CC-0001`.

---

## Cấu trúc thư mục (tại `b28-cart-state`)

```
cybercafe/
├── CyberCafe.slnx               # solution (định dạng XML mới)
├── global.json                  # pin .NET SDK 10
├── Directory.Build.props        # Nullable, ImplicitUsings cho mọi project
├── .github/workflows/ci.yml     # CI: restore → build → test
├── src/
│   ├── CyberCafe.Domain/        # C# thuần, không phụ thuộc web
│   │   ├── Products/            # Product → Drink → Coffee/Tea; Cake; DrinkSize
│   │   ├── Orders/              # Cart, Order, OrderItem, OrderStatus
│   │   ├── Discounts/           # Discount → MemberDiscount / VoucherDiscount
│   │   ├── Payments/            # Payment → Cash / Card / Momo
│   │   └── People/              # Person → Customer / Employee
│   └── CyberCafe.Web/           # Blazor Web App (Interactive Server)
│       ├── Components/Layout/   # MainLayout, NavMenu
│       ├── Components/Pages/    # Home, Menu, CartPage, Checkout, OrderConfirmation, About
│       ├── Components/Shared/   # ProductCard, CartSummary
│       ├── Models/              # CheckoutModel (form), AddToCartArgs
│       ├── Services/            # MenuService, DiscountService, OrderStore
│       └── State/               # CartState (scoped + event OnChange)
├── tests/CyberCafe.Tests/       # xUnit
├── docs/sessions/               # kịch bản live-code cho từng buổi
├── ROADMAP.md                   # kế hoạch các tag tiếp theo
└── README.md
```

---

## Bảng mốc

| Tag | Buổi | Nội dung chính | Trạng thái |
|-----|------|----------------|------------|
| `b23-blazor-start` | 23 | Blazor Web App, layout + routing, Razor syntax, `MenuService` + DI | ✅ |
| `b28-cart-state` | 24–31 | Component, `[Parameter]`, `EventCallback`, binding, lifecycle, `CartState`, `EditForm` + validation, domain OOP | ✅ |
| `b32-webapi-crud` | 32–35 | Tách `CyberCafe.Api`, typed `HttpClient`, CRUD thực đơn | ⏳ |
| `b34-signalr` | 33–34 | SignalR Hub: đơn mới realtime cho quầy barista | ⏳ |
| `b41-efcore` | 36–41 | SQL Server (Docker), EF Core Code First, migrations, LINQ | ⏳ |
| `b47-auth-filters` | 42–47 | JWT + BCrypt, middleware, filters, Redis cache, Repository + UoW | ⏳ |
| `b48-clean-arch` | 48 | Clean Architecture: Domain / Application / Infrastructure / Api | ⏳ |
| `b53-ddd-cqrs` | 49–53 | Order aggregate, CQRS | ⏳ |
| `b55-microservice` | 51–55 | .NET Aspire, YARP gateway, Order/Payment/Menu services, message broker | ⏳ |

Chi tiết từng mốc: [ROADMAP.md](ROADMAP.md). Kịch bản giảng: [docs/sessions/](docs/sessions/).

---

## Quy ước

- Định danh trong code bằng **tiếng Anh**; giao diện và chú thích bằng **tiếng Việt có dấu**.
- Mỗi mốc phải `dotnet build` + `dotnet test` xanh trước khi gắn tag.
- Commit message tiếng Anh, tiền tố theo buổi: `b28: ...`.

## License

[MIT](LICENSE) © TienNHM / tiennhm-edu
