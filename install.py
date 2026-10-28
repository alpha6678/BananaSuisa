#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Instalador do comando global `rb` (Ribanense Soluções) para Windows.

Depois de clonar (ou baixar) este repositório, basta rodar:

    python install.py

O script valida o ambiente e chama o instalador oficial
(``ferramentas\\install-rb-command.ps1``), que cria os shims
``rb.cmd``/``rb.ps1`` em ``%LOCALAPPDATA%\\RibanenseSolucoes\\bin`` e
adiciona essa pasta ao PATH do usuário (persistente, sem precisar de
admin) ou apenas da sessão atual (``--session``).

Uso:

    python install.py             # instala para o usuário (persistente)
    python install.py --session   # instala apenas no terminal atual
"""

from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
from pathlib import Path

BIN_DIR = Path.home() / "AppData" / "Local" / "RibanenseSolucoes" / "bin"
POWERSHELL = "powershell.exe"


def fail(mensagem: str) -> "NoReturn":  # type: ignore[valid-type]
    print(f"[ERRO] {mensagem}")
    sys.exit(1)


def verificar_ambiente(raiz: Path) -> None:
    if sys.platform != "win32":
        fail("Este instalador e para Windows. No Linux/macOS o rb nao e suportado.")

    if sys.version_info < (3, 8):
        fail(f"Python 3.8 ou mais novo e necessario (encontrado {sys.version.split()[0]}).")

    if shutil.which(POWERSHELL) is None:
        fail("Windows PowerShell (powershell.exe) nao encontrado no PATH.")

    for caminho, descricao in (
        (raiz / "rb.cmd", "rb.cmd na raiz"),
        (raiz / "ferramentas" / "install-rb-command.ps1", "install-rb-command.ps1"),
        (raiz / "ferramentas" / "Ribanense.cli.ps1", "Ribanense.cli.ps1"),
    ):
        if not caminho.is_file():
            fail(f"Arquivo esperado nao encontrado: {caminho} ({descricao}).")
            return  # inacessível; mantém o analisador estático feliz


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Instala o comando global rb (CLI do Ribanense Solucoes).",
    )
    parser.add_argument(
        "--session",
        action="store_true",
        help="instala apenas na sessao atual de terminal (nao persiste)",
    )
    args = parser.parse_args()

    raiz = Path(__file__).resolve().parent
    print("Ribanense Soluções — instalador do comando rb")
    print(f"Pasta do projeto: {raiz}")
    print()

    verificar_ambiente(raiz)

    escopo = "Session" if args.session else "User"
    instalador = raiz / "ferramentas" / "install-rb-command.ps1"
    comando = [
        POWERSHELL,
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        str(instalador),
        "-Scope",
        escopo,
    ]

    print(f"Executando instalador oficial (escopo {escopo})...")
    try:
        resultado = subprocess.run(comando)
    except KeyboardInterrupt:
        print()
        fail("Instalacao interrompida pelo usuario.")
    except OSError as erro:
        fail(f"Falha ao iniciar o PowerShell: {erro}")

    if resultado.returncode != 0:
        fail(f"O instalador oficial terminou com codigo {resultado.returncode}.")

    if (BIN_DIR / "rb.cmd").is_file():
        print()
        print(f"[OK] Shim confirmado em: {BIN_DIR}")
    else:
        print()
        print("[AVISO] Shim nao encontrado onde era esperado; veja as mensagens acima.")

    print()
    if args.session:
        print("Feche e abra um novo terminal (ou rode o comando sugerido acima)")
        print("e teste com:  rb help")
    else:
        print("Abra um NOVO terminal e teste com:  rb help")
    print()
    print("Se algum dia a pasta do projeto mudar de lugar, rode este")
    print("instalador novamente a partir do novo local.")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        print()
        sys.exit(130)
