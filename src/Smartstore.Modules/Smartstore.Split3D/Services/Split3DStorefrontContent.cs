namespace Smartstore.Split3D.Services;

/// <summary>
/// A sellable Split3D plan as created by the storefront setup.
/// </summary>
public sealed record Split3DPlanProduct(
    string Sku,
    string Plan,
    string Name,
    string Slug,
    decimal Price,
    string ImageFile,
    string Tagline,
    bool Featured);

/// <summary>
/// Describes a catalog product to create for one plan of an addon.
/// </summary>
public sealed class Split3DPlanProductSpec
{
    public string Plan { get; init; }
    public int? Days { get; init; }
    public string Sku { get; init; }
    public string Name { get; init; }
    public string Slug { get; init; }
    public decimal Price { get; init; }
    public string ShortDescription { get; init; }
    public string FullDescription { get; init; }
    public byte[] ImageBytes { get; init; }
    public string ImageFileName { get; init; }
    public bool ShowOnHomePage { get; init; }

    /// <summary>
    /// Devices a key bought with this package may activate. <c>null</c> uses the default from the settings.
    /// </summary>
    public int? MaxDevices { get; init; }
}

/// <summary>
/// Vietnamese storefront content (home page, product texts, topics) used by <see cref="Split3DStorefrontSetup"/>.
/// Customer-facing text is Vietnamese on purpose: the shop sells to Vietnamese customers.
/// </summary>
public static class Split3DStorefrontContent
{
    public const string StoreName = "Split3D Store";
    public const string CategoryName = "Công cụ 3D";

    /// <summary>
    /// The tools category under its current and earlier names ("Addon Blender" before 2026-10-02).
    /// </summary>
    public static readonly string[] CategoryNames = [CategoryName, "Addon Blender"];
    public const string CategorySlug = "addon-blender";
    public const string AddonVersion = "3.0.121";

    public static readonly Split3DPlanProduct[] Plans =
    [
        new("S3D-3M", Split3DPlans.ThreeMonths, "Split3D Print – Gói 3 tháng", "split3d-print-3-thang", 150000, "split3d-3-thang.png", "Dùng thử nghiêm túc cho một dự án", false),
        new("S3D-6M", Split3DPlans.SixMonths, "Split3D Print – Gói 6 tháng", "split3d-print-6-thang", 250000, "split3d-6-thang.png", "Tiết kiệm hơn cho nhu cầu đều đặn", false),
        new("S3D-1Y", Split3DPlans.OneYear, "Split3D Print – Gói 1 năm", "split3d-print-1-nam", 450000, "split3d-1-nam.png", "Lựa chọn của đa số người dùng", true),
        new("S3D-LT", Split3DPlans.Lifetime, "Split3D Print – Vĩnh viễn", "split3d-print-vinh-vien", 900000, "split3d-vinh-vien.png", "Mua một lần, dùng mãi mãi", false),
    ];

    public const string ShortDescription =
        "Addon Blender cắt mô hình quá khổ thành nhiều mảnh vừa bàn in 3D và tự tạo khớp nối để ráp lại chắc chắn. Giao diện Tiếng Việt / English.";

    public static string ProductFullDescription(Split3DPlanProduct plan, string version = null)
    {
        version = version.NullEmpty() ?? AddonVersion;
        var duration = plan.Plan switch
        {
            Split3DPlans.ThreeMonths => "90 ngày kể từ lúc cấp key",
            Split3DPlans.SixMonths => "180 ngày kể từ lúc cấp key",
            Split3DPlans.OneYear => "365 ngày kể từ lúc cấp key",
            _ => "Không giới hạn thời gian"
        };

        return $"""
            <h3>Split3D Print – addon Blender cho người in 3D</h3>
            <p>Cắt mô hình lớn thành các mảnh vừa bàn in, tự động tạo chốt và lỗ khớp nối để ráp lại khít và chắc. Tất cả làm ngay trong Blender, không cần phần mềm khác.</p>
            <h4>Bạn nhận được</h4>
            <ul>
              <li><strong>Key kích hoạt</strong> gửi qua email và hiển thị trong chi tiết đơn hàng.</li>
              <li><strong>File cài đặt addon</strong> (bản {version}) tải tại <em>Tài khoản › Tải về</em>.</li>
              <li><strong>Thời hạn:</strong> {duration}.</li>
              <li>Dùng được trên nhiều bản Blender trên cùng máy; nhập lại key khi đổi máy.</li>
            </ul>
            <h4>Tính năng chính</h4>
            <ul>
              <li>Vẽ đường cắt tự do, cắt vòng 360°, cắt theo vòng cạnh; xem trước trước khi áp dụng.</li>
              <li>Nhiều loại khớp nối: chốt tròn, chốt chữ D, núm kiểu LEGO, bản lề trục, chốt rời, hốc nam châm, khớp cầu.</li>
              <li>Tùy chỉnh kích thước, độ côn, khe hở dung sai, góc xoay cho từng khớp.</li>
              <li>Kiểm tra mảnh có vừa bàn in không, sửa lỗi lưới, tách rời (explode) và xuất file để in.</li>
              <li>Lịch sử thao tác, đơn vị mm / cm, giao diện Tiếng Việt và English.</li>
            </ul>
            <h4>Yêu cầu</h4>
            <ul>
              <li>Blender 4.2 trở lên (Windows, macOS, Linux).</li>
            </ul>
            <h4>Quy trình mua</h4>
            <ol>
              <li>Thêm gói vào giỏ và đặt hàng (cần đăng nhập để tải file).</li>
              <li>Chuyển khoản theo hướng dẫn, nội dung ghi <strong>mã đơn hàng</strong>.</li>
              <li>Khi xác nhận đã nhận tiền, hệ thống tự gửi key qua email và mở khóa file tải về.</li>
              <li>Cài addon trong Blender: <em>Edit › Preferences › Add-ons › Install from Disk</em>, dán key vào bảng Split3D và bấm Kích hoạt.</li>
            </ol>
            <p class="text-muted small">Split3D Print dựa trên mã nguồn Split3D Print của Cadaumoi, phát hành theo giấy phép GNU GPL v3.0. Gói tải về kèm đầy đủ mã nguồn và file LICENSE.</p>
            """;
    }

    /// <summary>
    /// Home page body. <paramref name="urls"/> maps SKU to product URL, <paramref name="prices"/> SKU to formatted price.
    /// </summary>
    public static string HomePage(IReadOnlyDictionary<string, string> urls, IReadOnlyDictionary<string, string> prices, string version = null)
    {
        version = version.NullEmpty() ?? AddonVersion;
        string Card(Split3DPlanProduct p, string period, string[] perks)
        {
            var featured = p.Featured ? " s3d-card--featured" : string.Empty;
            var ribbon = p.Featured ? "<div class=\"s3d-ribbon\">Phổ biến nhất</div>" : string.Empty;
            var items = string.Concat(perks.Select(x => $"<li>{x}</li>"));
            var btn = p.Featured ? "btn-primary" : "btn-outline-primary";

            return $"""
                <div class="s3d-card{featured}">
                  {ribbon}
                  <div class="s3d-card-plan">{p.Plan}</div>
                  <div class="s3d-card-price">{prices[p.Sku]}</div>
                  <div class="s3d-card-period">{period}</div>
                  <ul class="s3d-card-perks">{items}</ul>
                  <a class="btn {btn} btn-lg btn-block" href="{urls[p.Sku]}">Chọn gói này</a>
                </div>
                """;
        }

        var common = new[] { "Đầy đủ mọi tính năng", "Key gửi tự động qua email", "Tải file cài đặt trong tài khoản" };
        var p3 = Plans[0];
        var p6 = Plans[1];
        var p1y = Plans[2];
        var plt = Plans[3];

        return $$"""
            <style>
              .s3d-home { --s3d-blue:#2563eb; --s3d-ink:#0f172a; --s3d-muted:#64748b; --s3d-soft:#f1f5f9; color:var(--s3d-ink); font-size:1rem; font-weight:400; line-height:1.6; }
              .s3d-home section { padding:4rem 0; }
              .s3d-home h2 { font-weight:800; font-size:2rem; margin-bottom:.5rem; }
              .s3d-home .s3d-sub { color:var(--s3d-muted); font-size:1.1rem; max-width:720px; margin:0 auto 2.5rem; }
              .s3d-hero { background:radial-gradient(1200px 500px at 15% 0%, #1d4ed8 0%, #0b1a3a 60%, #070f24 100%); color:#fff; border-radius:1.25rem; padding:4.5rem 2.5rem !important; position:relative; overflow:hidden; }
              .s3d-hero:after { content:""; position:absolute; inset:0; background-image:linear-gradient(rgba(255,255,255,.05) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,.05) 1px, transparent 1px); background-size:48px 48px; pointer-events:none; }
              .s3d-hero > * { position:relative; z-index:1; }
              .s3d-hero h1 { font-size:clamp(2rem, 4.5vw, 3.4rem); font-weight:800; line-height:1.1; margin-bottom:1rem; }
              .s3d-hero p.lead { color:#cbd5e1; font-size:1.2rem; max-width:640px; }
              .s3d-badge { display:inline-block; background:rgba(255,255,255,.12); border:1px solid rgba(255,255,255,.2); color:#e0e7ff; padding:.35rem .9rem; border-radius:999px; font-size:.85rem; margin-bottom:1.25rem; }
              .s3d-hero .btn { margin:.25rem .5rem .25rem 0; }
              .s3d-hero-meta { margin-top:1.75rem; color:#94a3b8; font-size:.95rem; }
              .s3d-hero-meta span { margin-right:1.5rem; white-space:nowrap; }
              .s3d-hero-art { display:flex; align-items:center; justify-content:center; }
              .s3d-grid { display:grid; gap:1.25rem; grid-template-columns:repeat(auto-fit, minmax(240px, 1fr)); }
              .s3d-feature { background:#fff; border:1px solid #e2e8f0; border-radius:1rem; padding:1.5rem; height:100%; }
              .s3d-feature .s3d-ico { width:44px; height:44px; border-radius:.75rem; background:#eff6ff; color:var(--s3d-blue); display:flex; align-items:center; justify-content:center; font-size:1.35rem; margin-bottom:.9rem; }
              .s3d-feature h3 { font-size:1.1rem; font-weight:700; margin-bottom:.4rem; }
              .s3d-feature p { color:var(--s3d-muted); margin:0; }
              .s3d-steps { counter-reset:step; }
              .s3d-step { background:var(--s3d-soft); border-radius:1rem; padding:1.5rem; position:relative; }
              .s3d-step:before { counter-increment:step; content:counter(step); display:flex; align-items:center; justify-content:center; width:36px; height:36px; border-radius:50%; background:var(--s3d-blue); color:#fff; font-weight:700; margin-bottom:.75rem; }
              .s3d-step h3 { font-size:1.05rem; font-weight:700; }
              .s3d-step p { color:var(--s3d-muted); margin:0; font-size:.95rem; }
              .s3d-pricing { display:grid; gap:1.25rem; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); align-items:stretch; }
              .s3d-card { background:#fff; border:1px solid #e2e8f0; border-radius:1.25rem; padding:2rem 1.5rem; display:flex; flex-direction:column; position:relative; }
              .s3d-card--featured { border:2px solid var(--s3d-blue); box-shadow:0 20px 40px -20px rgba(37,99,235,.45); }
              .s3d-ribbon { position:absolute; top:-13px; left:50%; transform:translateX(-50%); background:var(--s3d-blue); color:#fff; font-size:.8rem; font-weight:700; padding:.25rem .9rem; border-radius:999px; white-space:nowrap; }
              .s3d-card-plan { font-weight:700; text-transform:uppercase; letter-spacing:.05em; color:var(--s3d-muted); font-size:.9rem; }
              .s3d-card-price { font-size:2rem; font-weight:800; margin:.5rem 0 0; }
              .s3d-card-period { color:var(--s3d-muted); margin-bottom:1.25rem; }
              .s3d-card-perks { list-style:none; padding:0; margin:0 0 1.5rem; flex-grow:1; }
              .s3d-card-perks li { padding:.35rem 0 .35rem 1.6rem; position:relative; }
              .s3d-card-perks li:before { content:"✓"; position:absolute; left:0; color:#16a34a; font-weight:800; }
              .s3d-req { background:var(--s3d-soft); border-radius:1.25rem; padding:2rem; }
              .s3d-faq details { border-bottom:1px solid #e2e8f0; padding:1rem 0; }
              .s3d-faq summary { font-weight:600; cursor:pointer; list-style:none; }
              .s3d-faq summary::-webkit-details-marker { display:none; }
              .s3d-faq summary:after { content:"+"; float:right; color:var(--s3d-blue); font-weight:700; }
              .s3d-faq details[open] summary:after { content:"–"; }
              .s3d-faq details p { color:var(--s3d-muted); margin:.75rem 0 0; }
              .s3d-cta { background:linear-gradient(135deg, #2563eb, #1e3a8a); color:#fff; border-radius:1.25rem; padding:3rem 2rem !important; text-align:center; }
              .s3d-cta p { color:#dbeafe; }
              .s3d-credit { color:var(--s3d-muted); font-size:.85rem; text-align:center; padding:1.5rem 0 0 !important; }
            </style>

            <div class="s3d-home">

              <section class="s3d-hero">
                <div class="row align-items-center">
                  <div class="col-lg-7">
                    <div class="s3d-badge">Addon Blender · Phiên bản {{version}} · Tiếng Việt / English</div>
                    <h1>In mô hình lớn hơn bàn in.<br>Cắt gọn, nối khít với Split3D Print.</h1>
                    <p class="lead">Chia mô hình quá khổ thành các mảnh vừa máy in 3D và tự động tạo chốt, lỗ khớp nối để ráp lại chắc chắn — làm tất cả ngay trong Blender.</p>
                    <a href="#s3d-pricing" class="btn btn-warning btn-lg font-weight-bold">Xem bảng giá</a>
                    <a href="{{urls[p1y.Sku]}}" class="btn btn-outline-light btn-lg">Mua gói 1 năm</a>
                    <div class="s3d-hero-meta">
                      <span>✓ Key gửi tự động</span><span>✓ Tải file ngay trong tài khoản</span><span>✓ Blender 4.2+</span>
                    </div>
                  </div>
                  <div class="col-lg-5 s3d-hero-art d-none d-lg-flex">
                    <svg viewBox="0 0 320 320" width="300" height="300" aria-hidden="true">
                      <g fill="none" stroke="#fff" stroke-width="2" stroke-linejoin="round">
                        <polygon points="160,40 260,95 160,150 60,95" fill="rgba(255,255,255,.28)"/>
                        <polygon points="60,95 160,150 160,195 60,140" fill="rgba(255,255,255,.12)"/>
                        <polygon points="260,95 160,150 160,195 260,140" fill="rgba(255,255,255,.2)"/>
                        <rect x="148" y="205" width="24" height="46" fill="#fbbf24" stroke="none"/>
                        <ellipse cx="160" cy="205" rx="12" ry="6" fill="#fde68a" stroke="none"/>
                        <polygon points="160,195 260,250 160,305 60,250" fill="rgba(255,255,255,.28)" transform="translate(0,-40)"/>
                        <polygon points="60,210 160,265 160,300 60,245" fill="rgba(255,255,255,.12)"/>
                        <polygon points="260,210 160,265 160,300 260,245" fill="rgba(255,255,255,.2)"/>
                      </g>
                    </svg>
                  </div>
                </div>
              </section>

              <section class="text-center">
                <h2>Mọi thứ bạn cần để in mô hình lớn</h2>
                <p class="s3d-sub">Từ vẽ đường cắt đến xuất file in — Split3D Print gói gọn quy trình chia mảnh vốn tốn hàng giờ thành vài cú nhấp.</p>
                <div class="s3d-grid text-left">
                  <div class="s3d-feature"><div class="s3d-ico">✂</div><h3>Cắt linh hoạt</h3><p>Vẽ đường cắt tự do, cắt vòng 360° hoặc theo vòng cạnh. Xem trước khung dây trước khi áp dụng.</p></div>
                  <div class="s3d-feature"><div class="s3d-ico">⚙</div><h3>Khớp nối đa dạng</h3><p>Chốt tròn, chốt chữ D, núm kiểu LEGO, bản lề trục, chốt rời, hốc nam châm và khớp cầu.</p></div>
                  <div class="s3d-feature"><div class="s3d-ico">↔</div><h3>Chỉnh dung sai chính xác</h3><p>Tùy chỉnh kích thước, độ côn, khe hở, góc xoay cho từng khớp để ráp vừa khít với máy của bạn.</p></div>
                  <div class="s3d-feature"><div class="s3d-ico">▦</div><h3>Kiểm tra bàn in</h3><p>Biết ngay mảnh nào vượt kích thước bàn in, sửa lỗi lưới trước khi xuất.</p></div>
                  <div class="s3d-feature"><div class="s3d-ico">⤢</div><h3>Tách rời &amp; xuất file</h3><p>Chế độ explode để xem tổng thể, xuất từng mảnh sẵn sàng cho phần mềm slicer.</p></div>
                  <div class="s3d-feature"><div class="s3d-ico">文</div><h3>Tiếng Việt / English</h3><p>Giao diện song ngữ, đơn vị mm/cm, lịch sử thao tác để quay lại bất cứ lúc nào.</p></div>
                </div>
              </section>

              <section class="text-center">
                <h2>Mua và kích hoạt trong 4 bước</h2>
                <p class="s3d-sub">Quy trình rõ ràng, key và file cài đặt được giao tự động ngay khi thanh toán được xác nhận.</p>
                <div class="s3d-grid s3d-steps text-left">
                  <div class="s3d-step"><h3>Chọn gói &amp; đặt hàng</h3><p>Chọn thời hạn phù hợp, thêm vào giỏ và đăng nhập / tạo tài khoản để đặt hàng.</p></div>
                  <div class="s3d-step"><h3>Chuyển khoản</h3><p>Chuyển khoản theo thông tin hiển thị khi thanh toán, nội dung ghi <strong>mã đơn hàng</strong>.</p></div>
                  <div class="s3d-step"><h3>Nhận key &amp; file</h3><p>Khi tiền về, key được gửi qua email và hiện trong đơn hàng; file addon mở khóa ở mục <em>Tải về</em>.</p></div>
                  <div class="s3d-step"><h3>Kích hoạt trong Blender</h3><p>Cài addon bằng <em>Install from Disk</em>, dán key vào bảng Split3D và bấm Kích hoạt.</p></div>
                </div>
              </section>

              <section id="s3d-pricing" class="text-center">
                <h2>Bảng giá</h2>
                <p class="s3d-sub">Tất cả các gói đều mở khóa đầy đủ tính năng. Thời hạn tính từ lúc cấp key.</p>
                <div class="s3d-pricing text-left">
                  {{Card(p3, "90 ngày sử dụng", common)}}
                  {{Card(p6, "180 ngày sử dụng", common)}}
                  {{Card(p1y, "365 ngày sử dụng", common.Append("Tiết kiệm nhất theo tháng").ToArray())}}
                  {{Card(plt, "Mua một lần · dùng mãi mãi", common.Append("Không cần gia hạn").ToArray())}}
                </div>
              </section>

              <section>
                <div class="s3d-req row">
                  <div class="col-md-6">
                    <h2>Yêu cầu hệ thống</h2>
                    <ul class="mb-0">
                      <li>Blender <strong>4.2</strong> trở lên</li>
                      <li>Windows, macOS hoặc Linux</li>
                      <li>Key dùng được cho nhiều bản Blender trên cùng máy</li>
                    </ul>
                  </div>
                  <div class="col-md-6">
                    <h2>Cài đặt nhanh</h2>
                    <ol class="mb-0">
                      <li>Tải <code>split3d_print.zip</code> ở mục Tải về</li>
                      <li>Blender › Edit › Preferences › Add-ons › Install from Disk</li>
                      <li>Mở bảng Split3D (phím N) › dán key › Kích hoạt</li>
                    </ol>
                  </div>
                </div>
              </section>

              <section class="s3d-faq">
                <h2 class="text-center">Câu hỏi thường gặp</h2>
                <p class="s3d-sub text-center">Chưa thấy câu trả lời? Liên hệ chúng tôi qua trang Liên hệ.</p>
                <details><summary>Bao lâu thì tôi nhận được key?</summary><p>Ngay khi chúng tôi xác nhận đã nhận được chuyển khoản (thường trong giờ làm việc), hệ thống tự gửi key qua email và hiển thị trong chi tiết đơn hàng của bạn.</p></details>
                <details><summary>Tôi tải file cài đặt ở đâu?</summary><p>Đăng nhập › Tài khoản của tôi › Tải về. File được mở khóa sau khi đơn hàng được thanh toán.</p></details>
                <details><summary>Thời hạn được tính từ khi nào?</summary><p>Thời hạn tính từ thời điểm key được cấp, không phải từ lần kích hoạt đầu tiên.</p></details>
                <details><summary>Đổi máy tính thì sao?</summary><p>Bạn chỉ cần cài lại addon và nhập lại cùng key. Key không bị tiêu hao sau khi kích hoạt.</p></details>
                <details><summary>Có dùng được cho nhiều bản Blender không?</summary><p>Có. Mỗi bản Blender lưu kích hoạt riêng; bạn nhập cùng một key hợp lệ cho từng bản.</p></details>
                <details><summary>Hết hạn rồi có gia hạn được không?</summary><p>Có, bạn chỉ cần mua gói mới và nhập key mới vào addon.</p></details>
              </section>

              <section class="s3d-cta">
                <h2>Sẵn sàng in những mô hình lớn hơn?</h2>
                <p class="mb-4">Chọn gói phù hợp và bắt đầu ngay hôm nay.</p>
                <a href="#s3d-pricing" class="btn btn-warning btn-lg font-weight-bold">Chọn gói ngay</a>
              </section>

              <section class="s3d-credit">
                Split3D Print dựa trên mã nguồn Split3D Print của Cadaumoi, phát hành theo giấy phép GNU GPL v3.0.
              </section>
            </div>
            """;
    }

    public static string PaymentDescription(string bankName, string accountNumber, string accountHolder)
    {
        string Value(string v) => System.Net.WebUtility.HtmlEncode(v.NullEmpty() ?? "(chưa cấu hình)");

        return $"""
            <p><strong>Chuyển khoản ngân hàng</strong> – đơn hàng được xử lý ngay khi chúng tôi xác nhận đã nhận tiền.</p>
            <ul>
              <li>Ngân hàng: <strong>{Value(bankName)}</strong></li>
              <li>Số tài khoản: <strong>{Value(accountNumber)}</strong></li>
              <li>Chủ tài khoản: <strong>{Value(accountHolder)}</strong></li>
              <li>Nội dung chuyển khoản: <strong>mã đơn hàng</strong> (hiển thị sau khi đặt hàng)</li>
            </ul>
            <p>Sau khi đặt hàng, bạn sẽ nhận mã QR quét được bằng app ngân hàng hoặc MoMo, đã điền sẵn số tiền và nội dung chuyển khoản.</p>
            <p>Sản phẩm in 3D được studio chuẩn bị và giao hàng; riêng addon Blender, key và file cài đặt được gửi tự động qua email.</p>
            """;
    }

    /// <summary>
    /// Phrases of earlier shipped texts that described the shop as addon-only (keys, no physical delivery).
    /// Stored pages and descriptions that still contain one are replaced by the current text (layout version 9).
    /// </summary>
    public static readonly string[] AddonOnlyPhrases =
    [
        "key và file cài đặt được gửi tự động ngay khi chúng tôi xác nhận đã nhận tiền.</p>",
        "Không có giao hàng vật lý.",
        "Tất cả sản phẩm là sản phẩm số"
    ];

    public const string ConditionsOfUse = """
        <h2>Điều khoản sử dụng</h2>
        <p>Khi đặt mua sản phẩm tại cửa hàng, bạn đồng ý với các điều khoản sau.</p>
        <h3>1. Sản phẩm in 3D</h3>
        <p>Sản phẩm in 3D (chậu cây, bảng tên, thời khoá biểu, keycap…) được studio in và hoàn thiện sau khi đơn hàng được xác nhận thanh toán. Sản phẩm tuỳ chỉnh được làm theo nội dung và thiết kế bạn nhập khi đặt hàng; vui lòng kiểm tra kỹ chữ, màu và kích thước trước khi đặt. Màu thực tế có thể chênh lệch nhẹ so với ảnh.</p>
        <h3>2. Giao hàng sản phẩm in 3D</h3>
        <p>Studio giao hàng qua đơn vị vận chuyển hoặc hẹn bạn nhận tại xưởng. Trạng thái đơn hiển thị trong <em>Tài khoản › Đơn hàng</em>.</p>
        <h3>3. Đổi trả sản phẩm in 3D</h3>
        <p>Sản phẩm bị lỗi in hoặc hư hỏng khi vận chuyển được in lại miễn phí; vui lòng liên hệ studio kèm ảnh trong vòng 3 ngày kể từ khi nhận hàng. Sản phẩm tuỳ chỉnh đã làm đúng nội dung bạn đặt không được đổi trả.</p>
        <h3>4. Addon Blender (sản phẩm số)</h3>
        <p>Addon được giao dưới dạng file tải về và key kích hoạt, không giao hàng vật lý. Key được gửi qua email, hiển thị trong <em>Tài khoản › Key của tôi</em>; file cài đặt được mở khoá trong mục Tải về sau khi đơn hàng được xác nhận thanh toán. Thời hạn tính từ lúc cấp key.</p>
        <p>Do đặc thù sản phẩm số, chúng tôi chỉ hoàn tiền addon khi key không thể kích hoạt và chúng tôi không khắc phục được trong vòng 7 ngày kể từ khi bạn liên hệ hỗ trợ.</p>
        <h3>5. Giấy phép mã nguồn</h3>
        <p>Split3D Print dựa trên mã nguồn Split3D Print của Cadaumoi và được phân phối theo giấy phép GNU General Public License v3.0. Gói tải về kèm đầy đủ mã nguồn và nội dung giấy phép; quyền của bạn theo GPL không bị hạn chế bởi các điều khoản này.</p>
        """;

    public const string ShippingInfo = """
        <h2>Giao hàng</h2>
        <h3>Sản phẩm in 3D</h3>
        <ul>
          <li>Studio in và hoàn thiện sau khi xác nhận thanh toán, và báo thời gian hoàn thiện khi xác nhận đơn.</li>
          <li>Giao hàng tận nơi qua đơn vị vận chuyển (thường 2–4 ngày) hoặc nhận tại xưởng.</li>
          <li>Trạng thái đơn hiển thị trong <em>Tài khoản › Đơn hàng</em>.</li>
        </ul>
        <h3>Addon Blender (sản phẩm số)</h3>
        <ul>
          <li>Không phát sinh phí vận chuyển.</li>
          <li>Key kích hoạt: gửi qua email và hiển thị trong <em>Tài khoản › Key của tôi</em>.</li>
          <li>File cài đặt: tải tại <em>Tài khoản › Tải về</em>, ngay khi thanh toán được xác nhận.</li>
        </ul>
        """;
}
