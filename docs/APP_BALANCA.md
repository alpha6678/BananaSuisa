# App Testador de Balanças

Guia técnico do app `Ribanense.Solucoes.App.Balanca`: como ele fala com balanças por
serial (COM física e USB-serial), quais protocolos suporta, como diagnosticar problemas
e como capturar dados de balanças novas para ampliar o suporte com segurança.

> Estado atual: o suporte foi **validado por fixtures e simulador**. A homologação com
> hardware físico (ao menos uma balança Toledo, Filizola e Urano, além de adaptadores
> USB-serial de famílias diferentes) permanece **pendente** e obrigatória antes de
> anunciar qualquer modelo como "homologado".

## Fluxo de uso: três passos

O app é um roteiro, não um painel de parâmetros. A varredura de combinações existe, mas
é o **plano B** — só entra em cena depois que a configuração documentada não responde.

1. **Porta do equipamento** — inventário das COMs presentes, no mesmo formato do
   Gerenciador de Dispositivos (`USB-SERIAL CH340 (COM5)`), com a origem de cada porta e
   se ela já está em uso por outro programa.
2. **Modelo da balança** — o usuário identifica a marca gravada no equipamento e o app
   aplica a configuração serial documentada daquele protocolo (ou o perfil já salvo).
3. **Testar a leitura** — `Ativar` → `Ler peso` / `Monitorar`. Se não responder, o app
   oferece **testar outras configurações** do mesmo modelo, restrito à porta escolhida.

Os parâmetros seriais individuais (baud, paridade, stop bits, handshake), a varredura em
todas as portas, a varredura profunda e o teste passo a passo ficam recolhidos no painel
**Avançado**, para os casos que fogem do documentado.

## Cenário de checkout: qual COM é a balança?

Num caixa é comum haver **duas ou três portas COM ao mesmo tempo** — tipicamente a
balança e a maquininha TEF. Por isso o app **lista também as portas Bluetooth** em vez de
escondê-las: elas precisam aparecer para serem reconhecidas e descartadas.

| O que aparece na lista | Origem (`SerialPortKind`) | Leitura prática |
|------------------------|---------------------------|-----------------|
| `Serial Padrão por link Bluetooth (COM3)` | `Bluetooth` | Normalmente é a maquininha TEF |
| `USB-SERIAL CH340 (COM5)`, `FTDI`, `CP210x`, `Prolific` | `UsbSerial` | Candidato a balança |
| `Porta de Comunicação (COM1)` (ACPI/PCI) | `Nativa` | Candidato a balança (DE-9 da placa) |
| Sem metadados WMI | `Desconhecida` | Testar como candidato |

A classificação vem do PNP ID e do nome do dispositivo (`SerialPortEnumerator.Classify`).
A sugestão inicial de porta descarta as Bluetooth e as já ocupadas, mas a escolha final é
sempre do usuário.

**Porta em uso**: antes de listar, o app tenta abrir cada COM e fechá-la imediatamente,
sem transmitir nada (`SerialPortEnumerator.IsPortBusy`). Acesso negado significa que outro
programa — o PDV, por exemplo — está com a porta aberta. As portas de link Bluetooth ficam
fora dessa sondagem: abri-las dispararia a conexão com o dispositivo pareado.

## Camadas de comunicação

- **RS-232 / TIA-232**: padrão elétrico clássico das balanças (níveis ±3..±15 V, DE-9).
- **UART**: a serialização assíncrona propriamente dita — `start bit + dados + paridade? + stop bit(s)`.
  A notação `8N1` significa 8 bits de dado, sem paridade (None), 1 stop bit.
- **USB-serial (CDC/ACM ou conversores FTDI, CH340, CP210x)**: aparecem no Windows como
  uma porta `COMx`. Deste ponto de vista, o app trata igual a uma COM física. **USB
  nativo/HID está fora do escopo atual** e será tratado em fase separada.

Uma porta COM de adaptador USB pode **mudar de número** ao ser reconectada; por isso o
enumerador guarda a identidade estável do dispositivo (PNP ID e VID/PID quando
disponíveis) — ver `SerialPortInfo.StableId`.

## Timing: por que às vezes parece "lento"

Serial "antiga" não é, por si só, o gargalo. O tempo de fio de uma resposta em `8N1` é:

```
tempo_fio (s) ≈ (nº de bytes) × 10 / baud
```

Exemplos para uma resposta curta de ~7 bytes:

| Baud  | Tempo de fio aproximado |
|-------|-------------------------|
| 9600  | ~7,3 ms                 |
| 2400  | ~29 ms                  |
| 110   | ~636 ms                 |

A esse tempo somam-se: processamento interno da balança, **buffering do driver/adaptador**
(o timer padrão de latência de um FTDI pode acrescentar até ~16 ms) e a política do app.
Por isso o leitor separa três orçamentos de tempo (`SerialReadOptions`):

- **primeiro byte** (`FirstByteTimeoutMs`): quanto esperar até a resposta começar;
- **intervalo entre bytes** (`InterByteTimeoutMs`): estimado a partir do baud, com folga;
- **teto total** (`TotalTimeoutMs`).

O monitor contínuo atualiza a UI em cadência (~150 ms) — isso é ritmo de exibição, não a
latência da serial.

## Framing e parsing

O parsing é **incremental** (`WeightFrameParser` + `IBalancaProtocol.Read`), devolvendo:

- `NeedMoreData`: frame ainda incompleto — acumula mais (não interpreta dígitos parciais);
- `FrameParsed`: frame reconhecido, com nº de bytes consumidos e **confiança** (`High` para
  delimitado STX/ETX, `Low` para texto salvo por heurística);
- `InvalidData`: ruído a descartar para **ressincronizar**.

Três conceitos ficam separados no resultado (`WeightReading`):

- `HasResponse`: a balança respondeu algo interpretável;
- `HasWeight`: veio um valor numérico;
- `IsUsable`: leitura **estável com valor** (inclui **zero estável**).

Assim, `IIIII`/`NNNNN`/`SSSSS` são respostas **válidas sem peso** (instável / negativo /
sobrecarga), distintas de timeout.

## Matriz de suporte / confiança

A coluna "Config sugerida" é o que o passo 2 aplica ao selecionar o modelo, vinda de
`IBalancaProtocol.DefaultConfig`. É também o primeiro candidato testado na varredura.

| Modelo        | Protocolo         | Config sugerida | Framing / formato                         | Confiança     |
|---------------|-------------------|-----------------|-------------------------------------------|---------------|
| Toledo (Prix/9094) | `ToledoProtocol`     | 9600 8N1 | ENQ → `STX PPPPP ETX`, 3 casas implícitas | Documentado   |
| Filizola      | `FilizolaProtocol`   | 9600 8N1 | ENQ → `STX PPPPP ETX`                      | Documentado   |
| Toledo 2180   | `Toledo2180Protocol` | 9600 8N1 | Linha term. CR, marcador `0x60` + 6 díg.  | Documentado¹  |
| Urano / Urano POP | `UranoProtocol`  | **9600 8N2** | STX/ETX **ou** texto `PESO: x,yzkg` | Documentado   |
| Automático / Genérico | `GenericHeuristicProtocol` | 9600 8N1 | Heurística (STX ou decimal explícito) | **Experimental** |
| LucasTec, Magna, Digitron, Magellan, Líder | `GenericHeuristicProtocol` | 9600 8N1 | Heurística (STX ou decimal explícito) | **Experimental** |
| Balança simulada | `GenericHeuristicProtocol` | 9600 8N1 (`COM-SIM`) | STX genérico | Demo/testes |

¹ Toledo 2180: reconhecemos um quadro por vez (com marcador = estável). A confirmação de
estabilidade por múltiplos quadros iguais, do firmware original, fica pendente de hardware.

Modelos "Experimental" **não são** suporte confirmado; são apenas o detector genérico com
outro rótulo, até existir fixture/manual e teste real.

## Varredura (scan)

- Só lista **portas presentes** (sem COM1–COM12 fantasmas).
- Por padrão a varredura fica **restrita à porta escolhida no passo 1**; abrir para todas
  as portas é uma opção explícita do painel Avançado (`ScanAllPorts`).
- Candidatos são **guiados pelo protocolo**: default do modelo primeiro, depois bauds
  documentados × formatos plausíveis. O modo **profundo** expande para o produto cartesiano.
- Timeout por tentativa é **adaptado ao baud** (bauds baixos ganham mais tempo).
- Para no primeiro **hit de alta confiança e estável** (configurável).
- Mostra **estimativa de duração** antes de iniciar e ranqueia por frame/confiança válida.
- O resultado escolhido (`Usar`) vira o perfil salvo do modelo (`ProfileStore`) e volta a
  ser a configuração aplicada no passo 2.
- Hot-plug: `SerialPortWatcher` (WMI `Win32_DeviceChangeEvent`) atualiza a lista e encerra
  a sessão quando a porta ativa desaparece.

## Troubleshooting

| Sintoma na UI/log                         | Causa provável                              |
|-------------------------------------------|---------------------------------------------|
| `sem resposta (timeout do primeiro byte)` | baud/porta errados, cabo, balança desligada |
| `resposta sem frame reconhecível`         | protocolo/formato errado para o modelo      |
| `erro de linha: paridade/framing/overrun` | paridade/data bits/stop incorretos ou ruído |
| `porta ocupada`                           | outro app usando a COM (a lista já marca "Em uso") |
| `porta inexistente` / `desconectada`      | adaptador removido ou nº de COM mudou        |

Dicas: confirme `DTR/RTS` (muitas balanças só transmitem com essas linhas ativas — o app
as liga quando não há handshake de hardware); para Urano lembre do padrão **8N2**; se a
porta escolhida for de link Bluetooth, provavelmente é a maquininha TEF e não a balança.

## Captura anônima para novos dispositivos

Para pedir suporte a um modelo novo, anexe uma captura anônima:

```
modelo: <marca/modelo declarado>
config: <ex.: COM3 9600 8N1> (a que respondeu)
comando_tx_hex: <bytes enviados, ex.: 05>
resposta_hex: <bytes recebidos, um exemplo por situação>
resposta_ascii: <mesma resposta em ASCII>
situacoes: estavel / instavel / negativo / sobrecarga / zero
observacoes: <manual, link, particularidades>
```

Não inclua dados sensíveis. Com raw hex de cada situação conseguimos criar uma fixture e um
perfil de protocolo verificável.
