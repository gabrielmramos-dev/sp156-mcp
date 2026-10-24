---
status: accepted
date: 2026-10-24
---

# Ler o XLSX com Sylvan.Data.Excel

## Contexto e problema

O arquivo real do SP156 (1º trimestre de 2025) tem 68 MB e 742.904 linhas com dado. A spec exige leitura em streaming, uma linha por vez, e a [ADR 0002](0002-privacidade-sem-enderecos.md) proíbe que endereço e coordenadas cheguem ao modelo ou ao banco. Precisamos de uma biblioteca de leitura que mantenha a memória baixa nesse arquivo.

## Fatores de decisão

* Leitura forward-only, sem materializar a planilha inteira.
* Pico de memória medido no arquivo real, não só a documentação das bibliotecas.
* Licença e manutenção.
* Nada que grave o conteúdo das células (por exemplo, endereços) em disco fora do nosso controle.

## Opções consideradas

* Sylvan.Data.Excel 0.5.8
* ExcelDataReader 3.9.0
* DocumentFormat.OpenXml 3.5.1 (`OpenXmlReader`)
* MiniExcel 1.46.0
* ClosedXML 0.105.1

ClosedXML foi descartada pela documentação, sem medição: carrega a planilha inteira em DOM e os próprios benchmarks dela mostram uso alto de memória. OpenXml e MiniExcel foram avaliadas só pela documentação e não entraram no spike.

## Resultado da decisão

Escolhida **Sylvan.Data.Excel**, com `ExcelDataReader` (3.9.0) como plano B.

### Números medidos

Spike descartável em [tools/spike-xlsx/](../../tools/spike-xlsx/): lê as 10 colunas úteis, mapeadas pelo nome do cabeçalho, de todas as linhas da aba `Solicitações` do arquivo real, e conta as linhas com dado. Cada execução foi um processo separado, com as bibliotecas em configuração padrão. Pico = `Process.PeakWorkingSet64` ao fim da iteração. Ambiente: .NET SDK 8.0.131, Linux, Intel Core i3-1115G4 (4 threads), 7 GiB de RAM.

| Biblioteca | Pico de working set | Tempo |
|---|---|---|
| Sylvan.Data.Excel 0.5.8 | 46, 47 e 53 MiB (3 execuções) | 7,3, 9,8 e 7,7 s |
| ExcelDataReader 3.9.0 | 62, 67, 68 e 87 MiB (4 execuções) | 20,2, 20,6, 30,8 e 32,2 s |

Em ambas, a contagem bateu com o [DATA.md](../DATA.md): 742.904 linhas; Finalizada 715.139, Aberto 23.504, Cancelada 3.948, Em Andamento 313. O Sylvan foi **cerca de 3 vezes mais rápido** e usou menos memória no pico. O ExcelDataReader variou bastante entre execuções (62 a 87 MiB). Com quatro ou menos execuções por biblioteca, os números dão ordem de grandeza e não estatística.

### O que o spike NÃO mediu

* Conversão de datas (serial do Excel para `DateOnly`).
* Normalização de `Assunto` e `Órgão`.
* Gravação no SQLite.
* O leitor definitivo com `Pedido`: o spike só lê strings e conta.
* Outros trimestres e anos, nem arquivos maiores que 68 MB.

O pico de memória do pipeline completo pode ser maior que o medido aqui e precisa ser remedido quando o leitor e a gravação existirem.

### Pontos de atenção

* **Licença:** o arquivo `license.txt` do repositório do Sylvan é MIT (Copyright 2026 Mark Pflug). Já o `licenseExpression` no NuGet está **vazio**, então ferramentas que leem só os metadados do pacote não veem a licença.
* **Atrito do ExcelDataReader:** em .NET 8 ele lança `NotSupportedException` ("No data is available for encoding 1252") até registrar `CodePagesEncodingProvider`. Além disso, seu modo `SpillToDisk` gravaria o conteúdo das células, endereços incluídos, em arquivos temporários de texto plano, o que conflita com o espírito da ADR 0002. Não foi necessário usá-lo: o pico sem ele ficou abaixo de 90 MiB.
* **Sylvan:** a primeira linha da aba é tratada como cabeçalho (nomes via `GetName`), o primeiro `Read()` já devolve dados, e linhas curtas exigem checar `RowFieldCount`. **Versão 0.x:** a API pode mudar entre versões, então fixamos a 0.5.8 e revisamos cada atualização. Manutenção de projeto de um só mantenedor: o autor diz que provavelmente não investiga issues sem arquivo de exemplo.

### Consequências

* Bom: menor pico de memória e leitura mais rápida no arquivo real.
* Ruim: dependência de um projeto com um mantenedor e versão 0.x. Se isso virar problema, o plano B é o ExcelDataReader.
* Ruim: a biblioteca parseia as células de endereço mesmo sem o código as usar. A ADR 0002 trata disso: o dado não é mapeado para o modelo nem gravado.

### Confirmação

Testes do leitor sobre a fixture e, opcionalmente, sobre o arquivo real (`SP156_XLSX`). Remedir o pico de memória do pipeline completo na tarefa de gravação.
