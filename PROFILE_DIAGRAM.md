# ConveyorTrackMeshBuilder — Profile Cross-Section

## Koordinat Sistemi
- **X ekseni** → sağa (oyuncu tarafı = +X yönü)
- **Y ekseni** → yukarı
- **Belt yüzeyi** = Y = 0
- Şema **önden** bakış (oyuncu tarafından)

---

## Profile Noktaları (P0 – P11)

```
Y
^
|
wa      P2----P3                    P8----P9
|      /        \                  /        \
wa-bv P1          P4--------------P7          P10
|     |           |                |           |
|     |  SOL      |                |   SAG     |
|     |  DUVAR    P5   [ BELT ]    P6  DUVAR   |
|     |           |                |           |
|     |           |                |           |
-rh   P0          |                |           P11
|
+-----+-----+-----+----------------+-----+-----+--> X
-(hw+rw) -(hw+rw)+bv  -hw-bv   -hw   0   hw  hw+bv  hw+rw
```

---

## Nokta Tablosu

| Nokta | X              | Y      | Açıklama                          |
|-------|----------------|--------|-----------------------------------|
| P0    | -(hw+rw)       | -rh    | Sol dış duvar — alt köşe          |
| P1    | -(hw+rw)       | wa-bv  | Sol dış duvar — üst (bevel altı)  |
| P2    | -(hw+rw)+bv    | wa     | Sol dış duvar — bevel yatay       |
| P3    | -hw-bv         | wa     | Sol iç duvar  — bevel yatay       |
| P4    | -hw            | wa-bv  | Sol iç duvar  — üst (bevel altı)  |
| P5    | -hw            | 0      | Belt sol kenar                    |
| P6    | +hw            | 0      | Belt sağ kenar                    |
| P7    | +hw            | wa-bv  | Sağ iç duvar  — üst (bevel altı)  |
| P8    | +hw+bv         | wa     | Sağ iç duvar  — bevel yatay       |
| P9    | +(hw+rw)-bv    | wa     | Sağ dış duvar — bevel yatay       |
| P10   | +(hw+rw)       | wa-bv  | Sağ dış duvar — üst (bevel altı)  |
| P11   | +(hw+rw)       | -rh    | Sağ dış duvar — alt köşe          |

---

## Edge (Kenar) Tablosu

| Edge | P başlangıç → P bitiş | Submesh | Gap'te ne olur?         |
|------|------------------------|---------|-------------------------|
| e=0  | P0 → P1               | Wall    | HİÇ KESİLMEZ (sol alt) |
| e=1  | P1 → P2               | Wall    | HİÇ KESİLMEZ           |
| e=2  | P2 → P3               | Wall    | HİÇ KESİLMEZ (sol üst) |
| e=3  | P3 → P4               | Wall    | HİÇ KESİLMEZ           |
| e=4  | P4 → P5               | Wall    | HİÇ KESİLMEZ (sol iç)  |
| e=5  | P5 → P6               | Belt    | HİÇ KESİLMEZ (belt)    |
| e=6  | P6 → P7               | Wall    | Şu an AÇIK bırakılıyor  |
| e=7  | P7 → P8               | Wall    | GAP'TE KESİLİYOR        |
| e=8  | P8 → P9               | Wall    | GAP'TE KESİLİYOR        |
| e=9  | P9 → P10              | Wall    | GAP'TE KESİLİYOR        |
| e=10 | P10 → P11             | Wall    | GAP'TE KESİLİYOR        |
| e=11 | P11 → P0              | Wall    | HİÇ KESİLMEZ (taban)   |

---

## Gap Bölgesindeki Yüzler (Şu Anki Durum)

```
ÖNDEN BAKIŞ — gap bölgesi (oyuncu tarafı)

      X eksen: sağ taraf (sag duvar)
      ← iç                          dış →
      hw                            hw+rw

wa-bv P7 ========================= P10   ← path yüzeyi üst kenarı
      |  [e=7..10 GAP'TE SİLİNDİ]  |
      |                              |
  0   P6                            P11   ← belt seviyesi / alt köşe
      |  [e=6 = iç yüz, AÇIK]       |
      |                              |
 -rh  (iç alt)                     P11   ← zemin
      |                              |
      +--------- CAP MESH -----------+
            (şu an outerX = P11.x'ten başlıyor)
```

---

## Şu Anki Cap Mesh Köşeleri

```
AddWallCap şu an kullanıyor:

  a = (innerX=P6.x,  y=0f )   ← P6 x koordinatı, belt seviyesi
  b = (outerX=P11.x, y=0f )   ← P11 x koordinatı, belt seviyesi
  c = (outerX=P11.x, y=-rh)   ← P11 (dış alt köşe)
  d = (innerX=P6.x,  y=-rh)   ← iç alt köşe

  Yani cap: P11.x (DIŞ KENAR) üzerinden kapatıyor
```

---

## Buraya kadar aç istediğin değişikliği:
> Cap mesh hangi noktalardan başlayıp nereye kapanacak?
> Hangi P noktaları kullanılacak?
