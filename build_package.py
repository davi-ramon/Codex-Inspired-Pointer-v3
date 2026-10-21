"""Draw each cursor preset from paths at high resolution; Pillow is build-only."""
from __future__ import annotations

import argparse
import io
import math
import struct
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent
ASSETS = ROOT / "presets" / "s85-b150" / "assets"
MOTION = ASSETS / "motion"
N, S = 128, 6
HOT = (12, 10)
SIZE_FACTOR = .425
OUTLINE_PX = 1.5


def place(x, y):
    return (N / 2 + (x - 32) * SIZE_FACTOR,
            N / 2 + (y - 32) * SIZE_FACTOR)


class Art:
    __slots__ = ("base", "hidpi")

    def __init__(self, base, hidpi):
        self.base, self.hidpi = base, hidpi


def make_edge_fade(n, width):
    """Let diffuse light reach full transparency before the cursor bitmap ends."""
    fade = Image.new("L", (n, n))
    values = []
    for y in range(n):
        for x in range(n):
            distance = min(x, y, n - 1 - x, n - 1 - y)
            t = min(1.0, distance / width)
            values.append(round(255 * t * t * (3 - 2 * t)))
    fade.putdata(values)
    return fade


EDGE_FADE = {scale: make_edge_fade(N*scale,14*scale) for scale in (1,2)}


def path(commands):
    points, at = [], (0, 0)
    for command in commands:
        op, *p = command
        if op == "M" or op == "L":
            at = (p[0], p[1]); points.append(at)
        elif op == "Q":
            x, y = at
            for i in range(1, 17):
                t = i / 16; u = 1 - t
                points.append((u*u*x + 2*u*t*p[0] + t*t*p[2],
                               u*u*y + 2*u*t*p[1] + t*t*p[3]))
            at = (p[2], p[3])
    return points


ARROW = path([
    ("M", 12.8, 9.8), ("Q", 10.4, 9.3, 11.2, 13.0),
    ("L", 18.6, 45.4), ("Q", 19.3, 49.2, 22.8, 49.8),
    ("Q", 25.8, 50.0, 27.5, 46.1), ("L", 32.7, 35.5),
    ("L", 43.9, 32.1), ("Q", 49.6, 30.7, 49.9, 27.8),
    ("Q", 50.2, 24.8, 46.6, 22.8), ("L", 15.8, 10.2),
    ("Q", 14.0, 9.5, 12.8, 9.8),
])
HAND = path([
    ("M", 25.9, 7.6), ("Q", 29.5, 7.5, 29.5, 11.5),
    ("L", 29.5, 25.6), ("Q", 32.3, 22.6, 34.5, 24.0),
    ("Q", 36.0, 24.9, 36.6, 27.3), ("Q", 39.5, 24.6, 41.6, 26.7),
    ("Q", 43.0, 27.8, 43.1, 29.7), ("Q", 46.0, 28.3, 47.8, 31.3),
    ("Q", 49.9, 34.9, 47.6, 40.7), ("L", 45.8, 45.1),
    ("Q", 43.8, 50.2, 39.0, 50.2), ("L", 26.0, 50.2),
    ("Q", 21.8, 49.7, 19.4, 45.8), ("L", 12.6, 35.0),
    ("Q", 10.2, 31.3, 13.5, 29.2), ("Q", 16.7, 27.2, 19.5, 30.1),
    ("L", 22.3, 32.6), ("L", 22.3, 11.5),
    ("Q", 22.5, 7.4, 25.9, 7.6),
])


def transformed_points(points, *, pivot=(32,32), angle=0, scale=1):
    a = math.radians(angle)
    c, s = math.cos(a), math.sin(a)
    out = []
    for x,y in points:
        x,y = (x-pivot[0])*scale,(y-pivot[1])*scale
        qx,qy=place(pivot[0]+c*x-s*y,pivot[1]+s*x+c*y)
        out.append((qx*S,qy*S))
    return out


def polygon(points, *, pivot=(32,32), angle=0, scale=1):
    m = Image.new("L", (N*S,N*S), 0)
    out = transformed_points(points, pivot=pivot, angle=angle, scale=scale)
    ImageDraw.Draw(m).polygon(out, fill=255)
    return m


def stroke(points, width=3):
    m = Image.new("L", (N*S,N*S), 0); d = ImageDraw.Draw(m)
    p = [(round(place(x,y)[0]*S),round(place(x,y)[1]*S)) for x,y in points]
    w=max(1,round(width*SIZE_FACTOR*S))
    d.line(p, fill=255, width=w, joint="curve")
    for x,y in p: d.ellipse((x-w//2,y-w//2,x+w//2,y+w//2),fill=255)
    return m


def union(*masks):
    result = Image.new("L",(N*S,N*S),0)
    for mask in masks: result = ImageChops.lighter(result,mask)
    return result


def polygon_rim(points):
    """Round vector outline before downsampling, so preset changes do not stair-step."""
    rim = Image.new("L", (N*S,N*S), 0)
    draw = ImageDraw.Draw(rim)
    outline_radius = round(OUTLINE_PX*S)
    draw.polygon(points, fill=255)
    draw.line(points+[points[0]], fill=255, width=outline_radius*2, joint="curve")
    for x,y in points:
        draw.ellipse((x-outline_radius,y-outline_radius,
                      x+outline_radius,y+outline_radius), fill=255)
    return rim


def theme(mask, glow=1, *, vector_points=None, red_glow=False):
    if vector_points is None:
        diameter = 2*round(OUTLINE_PX*S)+1
        rim_high = mask.filter(ImageFilter.MaxFilter(diameter))
    else:
        rim_high = polygon_rim(vector_points)
    spread = max(.8, min(1.45, math.sqrt(SIZE_FACTOR/.425)))
    colors = ((245,40,53),(255,102,112)) if red_glow else ((22,93,255),(74,164,255))

    def render(scale):
        n=N*scale
        core_alpha=mask.resize((n,n),Image.Resampling.LANCZOS)
        rim_alpha=rim_high.resize((n,n),Image.Resampling.LANCZOS)
        wide=core_alpha.filter(ImageFilter.MaxFilter(2*round(3*spread*scale)+1)).filter(
            ImageFilter.GaussianBlur(13*spread*scale))
        near=core_alpha.filter(ImageFilter.MaxFilter(2*round(2*spread*scale)+1)).filter(
            ImageFilter.GaussianBlur(5*spread*scale))
        result=Image.new("RGBA",(n,n),(0,0,0,0))
        for color,field,gain in [(colors[0],wide,.72*glow),
                                 (colors[1],near,.9*glow)]:
            layer=Image.new("RGBA",(n,n),color+(0,))
            halo_alpha=field.point(lambda a:min(255,round(a*gain)))
            layer.putalpha(ImageChops.multiply(halo_alpha,EDGE_FADE[scale]))
            result=Image.alpha_composite(result,layer)
        rim=Image.new("RGBA",(n,n),(255,255,255,0)); rim.putalpha(rim_alpha)
        core=Image.new("RGBA",(n,n),(0,0,0,0)); core.putalpha(core_alpha)
        return Image.alpha_composite(Image.alpha_composite(result,rim),core)

    return Art(render(1),render(2))


def arrow(angle=0, scale=1, glow=1):
    p = transformed_points(ARROW,pivot=HOT,angle=angle,scale=scale)
    return theme(polygon(ARROW,pivot=HOT,angle=angle,scale=scale),glow,
                 vector_points=p)


def hand(angle=0, scale=1, glow=1.1):
    p = transformed_points(HAND,pivot=(26,8),angle=angle,scale=scale)
    return theme(polygon(HAND,pivot=(26,8),angle=angle,scale=scale),glow,
                 vector_points=p)


def glyph(role):
    if role == "ibeam":
        m=union(stroke([(22,11),(42,11)],3),stroke([(32,11),(32,52)],3),
                stroke([(22,52),(42,52)],3))
    elif role == "crosshair":
        m=union(stroke([(32,8),(32,56)],3),stroke([(8,32),(56,32)],3))
    elif role == "no":
        circle=[(32+19*math.cos(i*math.pi/32),32+19*math.sin(i*math.pi/32)) for i in range(65)]
        m=union(stroke(circle,5),stroke([(18,46),(46,18)],5))
    elif role in ("sizens","sizewe","sizenwse","sizenesw"):
        a={"sizens":0,"sizewe":90,"sizenwse":-45,"sizenesw":45}[role]
        r=math.radians(a)
        def rot(x,y):
            x-=32;y-=32
            return (32+x*math.cos(r)-y*math.sin(r),32+x*math.sin(r)+y*math.cos(r))
        m=union(stroke([rot(32,17),rot(32,47)],3.5),
                polygon([rot(32,6),rot(22,21),rot(42,21)]),
                polygon([rot(32,58),rot(22,43),rot(42,43)]))
    elif role == "sizeall":
        m=union(stroke([(32,15),(32,49)],3),stroke([(15,32),(49,32)],3),
                polygon([(32,5),(23,20),(41,20)]),
                polygon([(32,59),(23,44),(41,44)]),
                polygon([(5,32),(20,23),(20,41)]),
                polygon([(59,32),(44,23),(44,41)]))
    elif role == "uparrow":
        m=polygon([(32,7),(8,31),(21,31),(21,55),(43,55),(43,31),(56,31)])
    elif role == "pen":
        m=union(stroke([(18,49),(44,15)],6),polygon([(11,56),(15,41),(26,49)]))
    elif role == "pin":
        circle=[(32+13*math.cos(i*math.pi/32),24+13*math.sin(i*math.pi/32)) for i in range(65)]
        m=union(stroke(circle,4),stroke([(21,32),(32,54),(43,32)],4))
    elif role == "person":
        circle=[(32+10*math.cos(i*math.pi/32),22+10*math.sin(i*math.pi/32)) for i in range(65)]
        m=union(polygon(circle),polygon([(15,52),(17,42),(23,36),(32,34),(41,36),(47,42),(49,52)]))
    else: raise ValueError(role)
    return theme(m,1.05,red_glow=(role == "no"))


def cursor(art,hotspot):
    # Windows normally chooses a 32 px cursor even when a CUR only contains
    # 128/256 px frames. Downsampling the whole canvas makes the actual glyph
    # nearly invisible. Small entries crop the transparent margin instead:
    # the glyph keeps its intended screen size, while larger entries retain
    # the full diffuse glow for high DPI displays and the motion helper.
    sizes=(32,48,64,96,128,256); parts=[]
    images=[]; hotspots=[]
    full=art.hidpi
    full_hot=tuple(round(v*256/N) for v in place(*hotspot))
    for n in sizes:
        if n>=128:
            img=art.base if n==128 else full
            hot=(round(full_hot[0]*n/256),round(full_hot[1]*n/256))
        else:
            crop=n*2
            left=(256-crop)//2; top=(256-crop)//2
            left=max(0,min(256-crop,full_hot[0]-4 if full_hot[0]<left+4 else
                           full_hot[0]-crop+4 if full_hot[0]>left+crop-4 else left))
            top=max(0,min(256-crop,full_hot[1]-4 if full_hot[1]<top+4 else
                          full_hot[1]-crop+4 if full_hot[1]>top+crop-4 else top))
            img=full.crop((left,top,left+crop,top+crop)).resize((n,n),Image.Resampling.LANCZOS)
            img.putalpha(ImageChops.multiply(img.getchannel("A"),
                                             make_edge_fade(n,max(3,n//8))))
            hot=(round((full_hot[0]-left)/2),round((full_hot[1]-top)/2))
        images.append(img); hotspots.append(hot)
    for img in images:
        buf=io.BytesIO()
        img.save(buf,"PNG",optimize=True)
        parts.append(buf.getvalue())
    result=bytearray(struct.pack("<HHH",0,2,len(sizes))); offset=6+16*len(sizes)
    for n,part,(hot_x,hot_y) in zip(sizes,parts,hotspots):
        result+=struct.pack("<BBBBHHII",n if n<256 else 0,n if n<256 else 0,0,0,
                            hot_x,hot_y,len(part),offset)
        offset+=len(part)
    return bytes(result)+b"".join(parts)


def save_cur(name,img,hotspot): (ASSETS/name).write_bytes(cursor(img,hotspot))


def chunk(name,data): return name+struct.pack("<I",len(data))+data+(b"\0" if len(data)%2 else b"")


def save_ani(name,frames,hotspot):
    icons=chunk(b"LIST",b"fram"+b"".join(chunk(b"icon",cursor(f,hotspot)) for f in frames))
    header=struct.pack("<9I",36,len(frames),len(frames),N,N,32,1,2,1)
    rates=struct.pack("<"+"I"*len(frames),*([2]*len(frames)))
    body=b"ACON"+chunk(b"anih",header)+chunk(b"rate",rates)+icons
    (ASSETS/name).write_bytes(b"RIFF"+struct.pack("<I",len(body))+body)


def spinner(phase,working):
    base=arrow(glow=1.25+.25*math.sin(phase*2*math.pi/30)) if working else Art(
        Image.new("RGBA",(N,N),(0,0,0,0)),Image.new("RGBA",(N*2,N*2),(0,0,0,0)))
    ratio=SIZE_FACTOR/.425
    overlay_hi=Image.new("RGBA",(N*S,N*S),(0,0,0,0)); d=ImageDraw.Draw(overlay_hi)
    cx,cy=place(58,54) if working else (64,64)
    r=(9 if working else 11)*ratio
    for i in range(10):
        speed=2 if not working else 1
        a=2*math.pi*(i+speed*phase/3)/10; x=cx+r*math.cos(a);y=cy+r*math.sin(a)
        dot=1.8*ratio*S
        d.ellipse((x*S-dot,y*S-dot,x*S+dot,y*S+dot),
                  fill=(225,243,255,round(50+195*(i/9)**2)))

    def compose(scale):
        n=N*scale
        frame=base.base if scale==1 else base.hidpi
        if not working:
            cloud=Image.new("L",(n,n),0)
            center=64*scale; radius=16*ratio*scale
            ImageDraw.Draw(cloud).ellipse((center-radius,center-radius,
                                           center+radius,center+radius),fill=190)
            cloud=cloud.filter(ImageFilter.GaussianBlur(13*ratio*scale))
            blue=Image.new("RGBA",(n,n),(30,110,255,0))
            blue.putalpha(ImageChops.multiply(cloud,EDGE_FADE[scale]))
            frame=Image.alpha_composite(frame,blue)
        overlay=overlay_hi.resize((n,n),Image.Resampling.LANCZOS)
        halo=Image.new("RGBA",(n,n),(35,127,255,0))
        halo_alpha=overlay.getchannel("A").filter(ImageFilter.GaussianBlur(5*scale))
        halo.putalpha(ImageChops.multiply(halo_alpha,EDGE_FADE[scale]))
        return Image.alpha_composite(Image.alpha_composite(frame,halo),overlay)

    return Art(compose(1),compose(2))


def main():
    save_cur("arrow.cur",arrow(),HOT)
    save_cur("hand.cur",hand(),(26,8))
    for role in ("ibeam","crosshair","no","sizens","sizewe","sizenwse",
                 "sizenesw","sizeall","uparrow","pen","pin","person"):
        hot=(32,7) if role=="uparrow" else ((11,56) if role=="pen" else (32,32))
        save_cur(role+".cur",glyph(role),hot)
    help_base=arrow()
    help_hi=Image.new("RGBA",(N*S,N*S),(0,0,0,0))
    d=ImageDraw.Draw(help_hi)
    bx,by=place(62,70); radius=7.5*SIZE_FACTOR/.425
    d.ellipse(((bx-radius)*S,(by-radius)*S,(bx+radius)*S,(by+radius)*S),
              fill=(30,95,220,255),outline=(255,255,255,255),width=2*S)
    try:
        badge_font=ImageFont.truetype("C:/Windows/Fonts/seguisb.ttf",round(10*S*SIZE_FACTOR/.425))
    except OSError:
        badge_font=ImageFont.load_default()
    d.text((bx*S,by*S),"?",font=badge_font,anchor="mm",fill=(255,255,255,255))
    help_img=Art(Image.alpha_composite(help_base.base,help_hi.resize((N,N),Image.Resampling.LANCZOS)),
                 Image.alpha_composite(help_base.hidpi,help_hi.resize((N*2,N*2),Image.Resampling.LANCZOS)))
    save_cur("help.cur",help_img,HOT)
    for a in range(-18,19,2):
        for s in range(-6,3):
            (MOTION/f"a{a:+03d}_s{s:+03d}.cur").write_bytes(
                cursor(arrow(a,1+s*.02,1+max(0,-s)*.08),HOT))
    for a in range(-12,13,4):
        for s in range(-6,3):
            (MOTION/f"h{a:+03d}_s{s:+03d}.cur").write_bytes(
                cursor(hand(a,1+s*.02,1.1+max(0,-s)*.08),(26,8)))
    working=[spinner(i,True) for i in range(30)]
    busy=[spinner(i,False) for i in range(30)]
    save_ani("working.ani",working,HOT);save_ani("busy.ani",busy,(32,32))
    samples=[
        ("Selecao normal",arrow()), ("Clique",arrow(scale=.88,glow=1.5)),
        ("Ajuda",help_img), ("Trabalhando",working[5]),
        ("Ocupado",busy[5]), ("Precisao",glyph("crosshair")),
        ("Texto",glyph("ibeam")), ("Manuscrito",glyph("pen")),
        ("Indisponivel",glyph("no")), ("Vertical",glyph("sizens")),
        ("Horizontal",glyph("sizewe")), ("Diagonal 1",glyph("sizenwse")),
        ("Diagonal 2",glyph("sizenesw")), ("Mover",glyph("sizeall")),
        ("Alternativa",glyph("uparrow")), ("Link",hand()),
        ("Link clicando",hand(scale=.88,glow=1.6)),
        ("Localizacao",glyph("pin")), ("Pessoa",glyph("person")),
    ]
    preview=Image.new("RGB",(1120,760),"#151923");draw=ImageDraw.Draw(preview)
    try:
        font=ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf",17)
        title_font=ImageFont.truetype("C:/Windows/Fonts/seguisb.ttf",25)
    except OSError:
        font=ImageFont.load_default();title_font=font
    draw.text((24,18),"Codex-inspired Pointer - conjunto completo",font=title_font,fill="white")
    draw.text((24,51),f"Tamanho {round(SIZE_FACTOR*200)}% | contorno {OUTLINE_PX:.1f}px | brilho sem recorte",font=font,fill="#b8c6e7")
    for i,(label,img) in enumerate(samples):
        x=24+(i%4)*273;y=84+(i//4)*130
        p=img.base.crop((8,8,120,120)).resize((100,100),Image.Resampling.LANCZOS)
        preview.paste(p,(x,y+8),p)
        draw.text((x+105,y+43),label,font=font,fill="white")
    preview.save(ASSETS.parent/"preview.png")
    print(f"{ASSETS.parent.name}: {len(list(MOTION.glob('*.cur')))} motion frames, 17 roles")


if __name__=="__main__":
    parser=argparse.ArgumentParser()
    parser.add_argument("--size",type=int,choices=(70,75,85,95,110,125,150),default=85)
    parser.add_argument("--border",type=int,choices=(130,150,170),default=150)
    args=parser.parse_args()
    SIZE_FACTOR=.5*args.size/100
    OUTLINE_PX=args.border/100
    ASSETS=ROOT/"presets"/f"s{args.size}-b{args.border}"/"assets"
    MOTION=ASSETS/"motion"
    MOTION.mkdir(parents=True,exist_ok=True)
    main()
