# Contribuindo com o BananaSuisa

Este documento define o fluxo minimo para evoluir o BananaSuisa na stack .NET/WPF atual.

## Fluxo rapido

1. Trabalhe nos arquivos C# e XAML dentro de `src/`.
2. Use `.\bs.cmd compilar`, `.\bs.cmd test` ou `.\bs.cmd check` ao trabalhar na base .NET.
3. Teste o projeto como administrador (executando o `.exe` gerado ou pelo Visual Studio elevado) quando a mudanca afetar `winget`, reparo, drivers, atualizacoes ou scripts do sistema.
4. Atualize a documentacao relacionada sempre que alterar comportamento, requisitos, comandos ou estrutura.

## Estrutura do codigo

| Local | Responsabilidade |
|-------|------------------|
| `BananaSuisa_recursos/` | Configs, catalogos base e memoria de execucao (`BananaSuisa_memoria`). |
| `ferramentas/` | CLI de desenvolvimento. |
| `src/` | Código fonte .NET (`App`, `Core`, `Services`, `Infrastructure`, `Shared`). |
| `BananaSuisa.slnx` | Solution base. |

## Regras praticas

- Preserve a separacao de camadas do .NET.
- Mudancas que afetem paths, memoria ou sincronizacao de dados devem considerar `BananaSuisa_recursos/BananaSuisa_memoria/`.
- Se tocar em `winget`, App Installer, drivers ou update do Windows, registe claramente o fluxo testado (lembre-se que requer permissão de Administrador).

## Validacao minima antes de concluir uma mudanca

1. Executar `.\bs.cmd compilar` e `.\bs.cmd test` ou `.\bs.cmd check`.
2. Abrir a aplicacao impactada com `.\bs.cmd run` e validar ao menos o fluxo alterado.
3. Se a mudanca afetar instalacao, remocao, reparo ou catalogo, testar com privilegios elevados.
4. Revisar os documentos afetados e atualizar links, exemplos e pre-requisitos.

## Como reportar bugs ou regressao

Inclua sempre que possivel:

- Versao do Windows.
- Se a execucao foi feita como administrador.
- Comando executado (`.\bs.cmd run`, `.\bs.cmd check`, etc.).
- Codigo de saida do `winget`, quando existir.
- Caminho do log: `BananaSuisa_recursos\BananaSuisa_memoria\Registros\BananaSuisa.json`.
- Passos para reproduzir e resultado esperado x obtido.

## Documentacao relacionada

- [`docs/INDICE.md`](docs/INDICE.md)
- [`docs/AMBIENTE.md`](docs/AMBIENTE.md)
- [`docs/FERRAMENTAS_CLI.md`](docs/FERRAMENTAS_CLI.md)
- [`docs/FERRAMENTAS_IA.md`](docs/FERRAMENTAS_IA.md)
- [`BananaSuisa_desenvolvimento/docs/ARQUITETURA.md`](BananaSuisa_desenvolvimento/docs/ARQUITETURA.md)
