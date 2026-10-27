# Rise Mode Aura Ice — protocolo do display

Este documento registra somente o que foi confirmado pelo código de
referência. Nenhum comando de ventoinha, bomba ou RGB foi inventado.

## Fontes

- [bmortella/wc-aura-ice-linux](https://github.com/bmortella/wc-aura-ice-linux):
  engenharia reversa original do display via USB/HID.
- [rafaelwms/win-watercooler-display](https://github.com/rafaelwms/win-watercooler-display):
  implementação Windows em C# que confirma o mesmo payload via HID nativo,
  sem WinUSB/Zadig.
- [liquidctl/liquidctl#781](https://github.com/liquidctl/liquidctl/issues/781):
  descritores USB do dispositivo e informação de que o mesmo VID/PID é usado
  por water coolers revendidos por outras marcas (por exemplo, Mancer).
- [rafaelwms/native-windows-hidsharp](https://github.com/rafaelwms/win-watercooler-display): mesma implementação Windows usada como fonte principal (ver acima).
- [Finallf/risemode](https://github.com/Finallf/risemode): confirma o VID/PID
  `0xAA88/0x8666` e envia uma escrita curta (`TEMP, 0x00` a cada ~2 s) via
  hidraw; funciona no Aura Ice Black 240/360 mm. Isso mostra que o firmware
  aceita escritas de tamanhos diferentes e usa o primeiro byte como
  temperatura.
- [Italord0/risemode-arch](https://github.com/Italord0/risemode-arch): mesma
  detecção por VID/PID e escrita curta de 1 byte (`TEMP`) a cada ~2 s,
  via hidraw.

Observação: existem escritas funcionais de tamanhos diferentes (1 byte, 2
bytes e 8 bytes de payload). O Aqua Control usa o formato de 8 bytes, que é o
confirmado no projeto Linux original e na implementação Windows de referência,
e no buffer do Windows é 9 bytes. Não há registro de escrita que leia dados do
dispositivo.

## Identificação

| Item | Valor |
|---|---|
| VID | `0xAA88` |
| PID | `0x8666` |
| Classe USB | HID (`0x03`) |
| Interface | 1 interface, configuração 1 |
| Endpoint IN | `0x82` (interrupt, 64 bytes) |
| Endpoint OUT | `0x02` (interrupt, 64 bytes) |
| Relatório de saída | 8 bytes de payload + Report ID 0 = 9 bytes no Windows |

O VID/PID é compartilhado por mais de um modelo/revenda. O Aqua Control não
identifica o dispositivo pelo nome comercial: exige VID/PID e um relatório de
saída de exatamente 9 bytes (Report ID + 8 bytes de payload). Ainda assim, um
outro water cooler com o mesmo controlador pode aceitar o mesmo protocolo; a
validação registrada abaixo é a do Aura Ice.

### Modelo-alvo

- Rise Mode Aura Ice ARGB 240 mm branco — `RM-WAI-02-ARGB`.

O projeto de referência foi testado em um **Aura Ice Black 360 mm ARGB**
(`RM-WAIB-06-ARGB`). A implementação Windows de referência usa o mesmo VID/PID
e o mesmo payload de 9 bytes. O controlador é o mesmo; a confirmação final no
modelo branco de 240 mm depende de teste com o hardware físico.

## Payload

O display recebe um relatório HID de saída. O payload de 8 bytes é:

```text
[TEMP, 0x1C, 0x00, 0x00, 0x1C, 0x00, 0x00, 0x00]
```

- `TEMP`: temperatura da CPU em °C, inteiro de 0 a 255. É o único byte cuja
  função foi confirmada.
- `0x1C` nas posições 1 e 4: valor fixo observado nas capturas. A função não
  foi confirmada; o driver mantém exatamente o valor da referência.
- Os demais bytes são zero.
- Não há checksum conhecido.

No Windows, a API HID inclui o Report ID como primeiro byte do buffer enviado
ao driver. Como o dispositivo usa Report ID `0x00`, o buffer completo é:

```text
[0x00, TEMP, 0x1C, 0x00, 0x00, 0x1C, 0x00, 0x00, 0x00]
```

## Inicialização

O script Linux original faz `reset()` e `set_configuration()` via pyusb, porque
estava falando diretamente com o endpoint USB. No Windows isso é responsabilidade
do driver HID do sistema; a implementação Windows de referência apenas abre o
dispositivo HID e escreve o relatório. O Aqua Control segue a implementação
Windows.

Não foi implementado o handshake `0x55 0xAA` citado em uma publicação sobre
outro projeto de engenharia reversa: esse handshake não aparece no repositório
de referência adotado nem na implementação Windows, e não há evidência de que
seja necessário para este display. Não enviamos comandos não confirmados.

## Atualização

- Intervalo de leitura/envio: **1 segundo**.
- O display faz transição suave da temperatura. Para a atualização ficar mais
  rápida, a referência envia o mesmo relatório **5 vezes**, com **50 ms** entre
  os envios.
- A temperatura é limitada a `0..255` antes de montar o pacote; valores fora
  desse intervalo são rejeitados pelo protocolo.

## Erros e reconexão

- Falha ao abrir o HID: a sessão não é adicionada e o dispositivo é procurado
  novamente a cada 5 segundos.
- Falha de escrita durante o monitoramento: a sessão é encerrada e liberada; o
  app volta a procurar um water cooler.
- O app controla um dispositivo por PC. O caminho HID (`DevicePath`) é a
  identidade da sessão; nenhuma outra sessão é aberta enquanto ela estiver
  ativa.
- Ausência de dispositivo não lança exceção para a interface.

## Funcionalidades comprovadamente suportadas

- Exibição da temperatura da CPU no display.

Não há evidência, nos projetos de referência, de comandos para ventoinhas,
bomba, RGB ou leitura de sensores do próprio water cooler. Esses recursos não
foram implementados.
