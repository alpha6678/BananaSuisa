# RibanenseESP

Casca de firmware da E32R28T-1. Docs: [`docs/FIRMWARE_RIBANENSEESP.md`](../../docs/FIRMWARE_RIBANENSEESP.md).

Versão: **0.0.1**. Pinout só o desta unidade — não copiar CYD clássico.

```bat
:: copiar para caminho sem acento se necessario
cd C:\fw\ribanense-esp
build_idf.bat build
build_idf.bat -p COM8 flash
```

No laboratório esta placa apareceu como `USB-SERIAL CH340 (COM8)`.
