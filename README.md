# Aqua Control — Display do watercooler Pichau Aqua 

Projeto para controlar o display do **Pichau Aqua X** e enviar a temperatura da CPU diretamente para o visor do watercooler.

<img width="1132" height="594" alt="Captura de tela 2026-09-03 132925" src="https://github.com/user-attachments/assets/eeed29b4-9daf-490b-bf00-96a22662d1d2" />

## Sobre o projeto

O Pichau Aqua 240X e suas variantes utilizam um controlador **WCH CH340** para comunicação USB, expondo o dispositivo como uma porta serial virtual.

Este projeto foi desenvolvido a partir da engenharia reversa do protocolo utilizado pelo software oficial do watercooler, o lineng tech.
A comunicação foi analisada através de capturas USB realizadas com:

- Wireshark
- USBPcap
- Software oficial do Pichau Aqua 240X
- Claude para verificar as leituras do USBPcap

---

## Identificação do dispositivo

| Informação      | Valor            |
| --------------- | ---------------- |
| Dispositivo     | Pichau Aqua 240X |
| Controlador     | WCH CH340        |
| VID             | `0x1A86`         |
| PID             | `0x484A`         |
| Interface       | Serial virtual   |
| Porta utilizada | `COM3`           |

---

## Protocolo identificado

Foi identificado um payload de **23 bytes** utilizado para atualizar o display:

```text
74 00 TT 08 26 XX YY ZZ 02 03 2E 02 02 02 02 02 02 02 02 02 30 1D
```

### Temperatura da CPU

O terceiro byte do pacote (`TT`) foi confirmado como o campo responsável pela temperatura da CPU.

```text
74 00 TT 08 26 XX YY ZZ ...
      ↑
  temperatura
```

A temperatura é enviada em hexadecimal.

Por exemplo:

```text
55 °C = 0x37
```

O pacote correspondente começaria com:

```text
74 00 37 08 26 ...
```

---

## Estrutura conhecida

|  Byte | Valor | Estado                 |
| ----: | ----- | ---------------------- |
|     0 | `74`  | Confirmado             |
|     1 | `00`  | Confirmado             |
|     2 | `TT`  | **Temperatura da CPU** |
|     3 | `08`  | Ainda não identificado |
|     4 | `26`  | Ainda não identificado |
|     5 | `XX`  | Ainda não identificado |
|     6 | `YY`  | Ainda não identificado |
|     7 | `ZZ`  | Ainda não identificado |
|     8 | `02`  | Ainda não identificado |
|     9 | `03`  | Ainda não identificado |
|    10 | `2E`  | Ainda não identificado |
| 11–20 | `02`  | Ainda não identificado |
|    21 | `30`  | Ainda não identificado |
|    22 | `1D`  | Ainda não identificado |

> **Nota:** os valores ainda não identificados foram obtidos através de capturas reais do software oficial e estão sendo utilizados como valores fixos durante os testes.

---

## O que ainda precisa ser descoberto

Apesar de o campo da temperatura já estar confirmado, ainda existem partes do protocolo que precisam ser analisadas, eu não tenho muito interesse nisso pois só quero o visor funcionando e como ele só monstra temperatura não me aprofundei muito.

Possíveis funções ainda não identificadas:

- RPM da bomba;
- estado do dispositivo;
- modo de operação;
- flags de controle;
- contador de pacotes;
- checksum;
- CRC;
- outros dados utilizados pelo controlador.

Os bytes finais `30 1D`, por exemplo, podem representar algum mecanismo de verificação de integridade, mas isso ainda não foi confirmado.

---

## Comunicação

Atualmente o projeto utiliza a porta serial virtual:
COM3

O software oficial do watercooler deve estar **fechado** durante os testes, pois ele utiliza a mesma porta e pode impedir o acesso ao dispositivo.

## Permissões

O programa requer execução como **Administrador**.
A solicitação de privilégios já está configurada através do:

app.manifest

## Engenharia reversa

O protocolo foi descoberto através da comparação de diferentes pacotes enviados pelo software oficial



## Status do projeto

### Confirmado

- [x] Identificação do controlador USB
- [x] VID/PID
- [x] Comunicação através de porta serial virtual
- [x] Identificação da `COM3`
- [x] Captura dos pacotes USB
- [x] Identificação do payload
- [x] Identificação do byte da temperatura
- [x] Envio da temperatura para o display


---



Ao iniciar, a aplicação tenta conectar automaticamente à porta fixa `COM3` e tenta reconectar a cada 5 segundos quando o dispositivo não está disponível.

O botão de conexão permite iniciar ou interromper o monitoramento. A interface também exibe as temperaturas mínima, média e máxima.



Quando a temperatura da CPU atinge `90 °C`, o Windows exibe uma notificação de temperatura alta. O alerta não se repete enquanto a temperatura permanecer acima desse limite e é liberado novamente quando ela cai para `80 °C` ou menos.

O software oficial do watercooler deve estar **fechado** durante os testes, pois ele utiliza a mesma porta e pode impedir o acesso ao dispositivo.

### Windows Defender

Dependendo das configurações de segurança do Windows, o **Windows Defender ou outro antivírus pode exibir um alerta durante a execução**.

Isso pode acontecer porque o programa acessa APIs e recursos do sistema operacional para obter a temperatura da CPU informada pelo próprio Windows.

O projeto não possui a intenção de realizar nenhuma atividade maliciosa. O acesso a recursos do sistema é necessário para obter as informações de hardware e enviá-las ao display do watercooler.

Como o projeto ainda está em desenvolvimento e não possui um instalador ou assinatura digital, alguns mecanismos de segurança podem classificar o executável como potencialmente suspeito.

Compatibilidade

Atualmente, o protocolo foi confirmado utilizando um Pichau Aqua 240X.

A compatibilidade com outros modelos da linha Aqua, como Aqua 120X e Aqua 360X, ainda não foi confirmada

[Ícone utilizado — Flaticon](https://www.flaticon.com/free-icon/sea_8312504)

