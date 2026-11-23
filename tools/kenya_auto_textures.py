"""
Pure-Python High-Resolution Procedural Texture Generator for Kenya Street Sim Blender 5.3 Studio.
Zero external dependencies (uses only stdlib zlib + struct).
Generates crisp Kenyan license plates, glowing LED route boards, 80 KPH stickers,
red/white diagonal commercial reflector chevrons, and Savanna Express / Parcel liveries!
"""

import os
import struct
import zlib

# Clean 5x7 pixel bitmap font for crisp Kenyan number plates, LED destination signs & decals
FONT_5X7 = {
    'A': ["01110", "10001", "10001", "11111", "10001", "10001", "10001"],
    'B': ["11110", "10001", "10001", "11110", "10001", "10001", "11110"],
    'C': ["01111", "10000", "10000", "10000", "10000", "10000", "01111"],
    'D': ["11110", "10001", "10001", "10001", "10001", "10001", "11110"],
    'E': ["11111", "10000", "10000", "11110", "10000", "10000", "11111"],
    'F': ["11111", "10000", "10000", "11110", "10000", "10000", "10000"],
    'G': ["01111", "10000", "10000", "10111", "10001", "10001", "01110"],
    'H': ["10001", "10001", "10001", "11111", "10001", "10001", "10001"],
    'I': ["01110", "00100", "00100", "00100", "00100", "00100", "01110"],
    'J': ["00111", "00010", "00010", "00010", "10010", "10010", "01100"],
    'K': ["10001", "10010", "10100", "11000", "10100", "10010", "10001"],
    'L': ["10000", "10000", "10000", "10000", "10000", "10000", "11111"],
    'M': ["10001", "11011", "10101", "10101", "10001", "10001", "10001"],
    'N': ["10001", "11001", "10101", "10011", "10001", "10001", "10001"],
    'O': ["01110", "10001", "10001", "10001", "10001", "10001", "01110"],
    'P': ["11110", "10001", "10001", "11110", "10000", "10000", "10000"],
    'Q': ["01110", "10001", "10001", "10001", "10101", "10010", "01101"],
    'R': ["11110", "10001", "10001", "11110", "10100", "10010", "10001"],
    'S': ["01111", "10000", "10000", "01110", "00001", "00001", "11110"],
    'T': ["11111", "00100", "00100", "00100", "00100", "00100", "00100"],
    'U': ["10001", "10001", "10001", "10001", "10001", "10001", "01110"],
    'V': ["10001", "10001", "10001", "10001", "10001", "01010", "00100"],
    'W': ["10001", "10001", "10001", "10101", "10101", "11011", "10001"],
    'X': ["10001", "10001", "01010", "00100", "01010", "10001", "10001"],
    'Y': ["10001", "10001", "01010", "00100", "00100", "00100", "00100"],
    'Z': ["11111", "00001", "00010", "00100", "01000", "10000", "11111"],
    '0': ["01110", "10001", "10001", "10001", "10001", "10001", "01110"],
    '1': ["00100", "01100", "00100", "00100", "00100", "00100", "01110"],
    '2': ["01110", "10001", "00001", "00110", "01000", "10000", "11111"],
    '3': ["11110", "00001", "00001", "01110", "00001", "00001", "11110"],
    '4': ["00010", "00110", "01010", "10010", "11111", "00010", "00010"],
    '5': ["11111", "10000", "11110", "00001", "00001", "10001", "01110"],
    '6': ["01110", "10000", "11110", "10001", "10001", "10001", "01110"],
    '7': ["11111", "00001", "00010", "00100", "01000", "01000", "01000"],
    '8': ["01110", "10001", "10001", "01110", "10001", "10001", "01110"],
    '9': ["01110", "10001", "10001", "01111", "00001", "00001", "01110"],
    '-': ["00000", "00000", "00000", "11111", "00000", "00000", "00000"],
    '.': ["00000", "00000", "00000", "00000", "00000", "01100", "01100"],
    '•': ["00000", "00000", "00100", "01110", "00100", "00000", "00000"],
    ' ': ["00000", "00000", "00000", "00000", "00000", "00000", "00000"],
}


class Canvas:
    def __init__(self, w, h, bg=(240, 240, 240)):
        self.w = w
        self.h = h
        self.buf = bytearray(bg * (w * h))

    def fill_rect(self, x0, y0, x1, y1, col):
        x0 = max(0, int(x0))
        y0 = max(0, int(y0))
        x1 = min(self.w, int(x1))
        y1 = min(self.h, int(y1))
        r, g, b = col
        row_bytes = bytes((r, g, b)) * max(0, x1 - x0)
        if not row_bytes:
            return
        for y in range(y0, y1):
            idx = (y * self.w + x0) * 3
            self.buf[idx:idx + len(row_bytes)] = row_bytes

    def draw_border(self, pad, thick, col):
        self.fill_rect(pad, pad, self.w - pad, pad + thick, col)
        self.fill_rect(pad, self.h - pad - thick, self.w - pad, self.h - pad, col)
        self.fill_rect(pad, pad, pad + thick, self.h - pad, col)
        self.fill_rect(self.w - pad - thick, pad, self.w - pad, self.h - pad, col)

    def draw_text_centered(self, text, cy, scale_x, scale_y, col, spacing=None):
        text = text.upper()
        if spacing is None:
            spacing = max(2, int(scale_x * 1.4))
        char_w = 5 * scale_x
        total_w = len(text) * char_w + max(0, len(text) - 1) * spacing
        sx = (self.w - total_w) // 2
        sy = int(cy - (7 * scale_y) // 2)
        for ch in text:
            glyph = FONT_5X7.get(ch, FONT_5X7[' '])
            for gy, row in enumerate(glyph):
                for gx, bit in enumerate(row):
                    if bit == '1':
                        self.fill_rect(
                            sx + gx * scale_x,
                            sy + gy * scale_y,
                            sx + (gx + 1) * scale_x,
                            sy + (gy + 1) * scale_y,
                            col,
                        )
            sx += char_w + spacing

    def save_png(self, path):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        raw = bytearray()
        stride = self.w * 3
        for y in range(self.h):
            raw.append(0)
            row_start = y * stride
            raw.extend(self.buf[row_start:row_start + stride])

        def chunk(tag, data):
            return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

        ihdr = struct.pack(">IIBBBBB", self.w, self.h, 8, 2, 0, 0, 0)
        idat = zlib.compress(bytes(raw), 6)
        png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", idat) + chunk(b"IEND", b"")
        with open(path, "wb") as f:
            f.write(png)
        return path


def make_kenya_plate_png(path, plate_text, yellow=True):
    bg = (244, 198, 20) if yellow else (242, 244, 248)
    c = Canvas(512, 128, bg=bg)
    c.draw_border(6, 7, (18, 20, 24))
    # Kenyan flag accent mini-bar on left side of modern generation plate!
    c.fill_rect(18, 22, 34, 48, (18, 18, 20))
    c.fill_rect(18, 48, 34, 78, (186, 22, 28))
    c.fill_rect(18, 78, 34, 106, (16, 118, 52))
    c.draw_text_centered(plate_text, 66, scale_x=9, scale_y=11, col=(14, 16, 20), spacing=10)
    return c.save_png(path)


def make_led_board_png(path, line_text, amber=True):
    c = Canvas(1024, 160, bg=(12, 14, 18))
    c.draw_border(4, 5, (32, 36, 44))
    col = (255, 178, 18) if amber else (64, 242, 128)
    scale_x = 6 if len(line_text) > 22 else 8
    c.draw_text_centered(line_text, 82, scale_x=scale_x, scale_y=11, col=col, spacing=max(4, scale_x))
    return c.save_png(path)


def make_chevron_bar_png(path):
    """Authentic Kenyan commercial vehicle red/yellow reflective diagonal safety chevron bar."""
    c = Canvas(512, 64, bg=(242, 204, 18))
    for y in range(64):
        for x in range(512):
            if ((x + y) // 36) % 2 == 0:
                idx = (y * 512 + x) * 3
                c.buf[idx:idx + 3] = bytes((214, 22, 22))
    c.draw_border(0, 3, (26, 26, 30))
    return c.save_png(path)


def make_speed_sticker_png(path, speed_text="80 KPH"):
    c = Canvas(256, 256, bg=(244, 246, 248))
    # Thick red circular speed-limit border
    cx, cy = 128, 128
    for y in range(256):
        for x in range(256):
            d2 = (x - cx) ** 2 + (y - cy) ** 2
            if 96 * 96 <= d2 <= 122 * 122:
                idx = (y * 256 + x) * 3
                c.buf[idx:idx + 3] = bytes((210, 20, 24))
    c.draw_text_centered("80", 104, scale_x=11, scale_y=12, col=(16, 18, 22), spacing=10)
    c.draw_text_centered("KPH", 174, scale_x=6, scale_y=6, col=(180, 20, 24), spacing=6)
    return c.save_png(path)


def make_side_banner_png(path, title, subtitle, bg_col, accent_col, text_col=(250, 250, 252)):
    c = Canvas(1024, 256, bg=bg_col)
    c.fill_rect(0, 0, 1024, 22, accent_col)
    c.fill_rect(0, 234, 1024, 256, accent_col)
    c.draw_text_centered(title, 100, scale_x=9, scale_y=12, col=text_col, spacing=10)
    c.draw_text_centered(subtitle, 188, scale_x=5, scale_y=7, col=accent_col, spacing=7)
    return c.save_png(path)


def ensure_all_textures(tex_dir):
    os.makedirs(tex_dir, exist_ok=True)
    return {
        "plate_matatu":   make_kenya_plate_png(os.path.join(tex_dir, "Plate_KDG412B.png"), "KDG 412B", yellow=True),
        "plate_probox":   make_kenya_plate_png(os.path.join(tex_dir, "Plate_KCS704L.png"), "KCS 704L", yellow=False),
        "plate_prado":    make_kenya_plate_png(os.path.join(tex_dir, "Plate_KDP900K.png"), "KDP 900K", yellow=False),
        "plate_hilux":    make_kenya_plate_png(os.path.join(tex_dir, "Plate_KDM285G.png"), "KDM 285G", yellow=True),
        "plate_saloon":   make_kenya_plate_png(os.path.join(tex_dir, "Plate_KCT518N.png"), "KCT 518N", yellow=False),
        "plate_canter":   make_kenya_plate_png(os.path.join(tex_dir, "Plate_KDL630E.png"), "KDL 630E", yellow=True),
        "plate_coach":    make_kenya_plate_png(os.path.join(tex_dir, "Plate_KDX057M.png"), "KDX 057M", yellow=True),
        "plate_lorry":    make_kenya_plate_png(os.path.join(tex_dir, "Plate_KDX409G.png"), "KDX 409G", yellow=True),
        "led_matatu":     make_led_board_png(os.path.join(tex_dir, "LED_Matatu_Route.png"), "NAIROBI - NAKURU - KISUMU", amber=True),
        "led_coach":      make_led_board_png(os.path.join(tex_dir, "LED_Savanna_Coach.png"), "SAVANNA EXPRESS - MOMBASA", amber=True),
        "chevron":        make_chevron_bar_png(os.path.join(tex_dir, "Kenya_Chevron_Bar.png")),
        "speed_80":       make_speed_sticker_png(os.path.join(tex_dir, "Kenya_80KPH.png")),
        "banner_coach":   make_side_banner_png(os.path.join(tex_dir, "Livery_Savanna_Coach.png"), "SAVANNA EXPRESS", "KENYA EXECUTIVE LUXURY COACH", (116, 16, 28), (242, 188, 32)),
        "banner_parcel":  make_side_banner_png(os.path.join(tex_dir, "Livery_Savanna_Parcel.png"), "SAVANNA PARCEL", "NATIONWIDE EXPRESS FREIGHT", (236, 240, 246), (22, 86, 174), text_col=(18, 44, 96)),
        "banner_matatu":  make_side_banner_png(os.path.join(tex_dir, "Livery_Matatu_Sacco.png"), "HIGHLAND SACCO", "NAIROBI EXPRESS 14-SEATER", (246, 192, 12), (16, 98, 118), text_col=(16, 24, 32)),
    }


if __name__ == "__main__":
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "KenyaStreetSim", "BlenderStudio", "Textures")
    res = ensure_all_textures(out)
    print(f"GENERATED {len(res)} CRISP KENYAN AUTOMOTIVE TEXTURES IN: {out}")
