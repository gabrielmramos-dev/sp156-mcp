# sp156-mcp

Servidor [MCP](https://modelcontextprotocol.io) **não oficial**, em C#, sobre os dados abertos do SP156 (Central de Atendimento da Prefeitura de São Paulo).

> **Este projeto não é oficial.** Não tem vínculo, afiliação nem endosso da Prefeitura de São Paulo nem da Prodam.

## Estado

- **Marco 1: ingestão concluída.** Lê o XLSX do SP156 e grava os pedidos em um banco SQLite, sem nenhuma coluna de endereço ou coordenada. Há um comando de linha de comando (`ingerir`) para isso.
- **Marco 2: servidor MCP, ainda não iniciado.** Não existe servidor MCP nem ferramentas.

## O que pretende ser

Permitir que um assistente de IA responda perguntas agregadas sobre pedidos feitos ao SP156 (quantos, onde, quando, tendência), sempre acompanhadas das ressalvas dos dados. Exemplo de pergunta: "quais distritos mais pediram remoção de entulho no 1º trimestre de 2025?". Isso é o objetivo do Marco 2, não algo que já funciona.

## Requisitos

SDK do .NET na versão indicada em [`global.json`](global.json) (hoje 10.0.115, com `rollForward: latestFeature`). Ver o [ADR 0006](docs/decisions/0006-migrar-para-dotnet-10.md).

## Como obter o dado

Os dados vêm do portal de dados abertos da Prefeitura de São Paulo, no conjunto de dados do SP156: <https://dados.prefeitura.sp.gov.br/dataset/dados-do-sp156>. A página de [Dados Abertos – SP156](https://transparencia.prefeitura.sp.gov.br/dados-abertos-sp156/), do Portal da Transparência, descreve o conjunto e traz notas de esclarecimento. O arquivo usado para desenvolver e verificar este projeto é `arquivo-final-1-trim-25.xlsx` (1º trimestre de 2025). Estrutura, limitações e divergências em relação à documentação oficial: [docs/DATA.md](docs/DATA.md).

Dados reais **nunca** entram no git (`.gitignore`). A única planilha versionada é a fixture fictícia em `tests/.../Fixtures/`.

## Ingerir um arquivo

Lê a aba `Solicitações` de um XLSX do SP156 e grava os pedidos em um banco SQLite:

```bash
dotnet run --project src/Sp156.Cli -- ingerir <arquivo.xlsx> <saida.db>
```

- O arquivo de saída **não pode existir**: o comando nunca sobrescreve nada. A gravação vai para `<saida.db>.tmp` e é renomeada só no fim, então uma falha não deixa um banco pela metade.
- Em caso de sucesso imprime `<N> pedidos gravados em <saida.db>` e termina com código `0`.
- Erro esperado (arquivo inexistente, destino já existente, coluna ou aba ausente, linha inválida, `.tmp` de uma gravação anterior): mensagem na saída de erro e código `1`.
- Arquivo corrompido ou que não é um XLSX: `Erro inesperado ao processar '<caminho>' (<TipoDaExceção>). O arquivo pode estar corrompido ou não ser um XLSX.` e código `1`, sem a mensagem da biblioteca e sem stack trace.
- O destino é checado antes de abrir o XLSX, então um destino já existente é o primeiro erro apontado.
- Argumentos errados: texto de uso na saída de erro e código `2`.
- As mensagens trazem só caminhos, nomes de coluna e número de linha, nunca o conteúdo das células. O esquema do banco está na [spec](docs/spec-m1-ingestao.md).

## O que está garantido e como foi verificado

- **O banco não tem coluna de endereço nem de coordenadas** (Logradouro, Número, CEP, Latitude, Longitude). Essas colunas não são mapeadas para o modelo nem gravadas ([ADR 0002](docs/decisions/0002-privacidade-sem-enderecos.md)). Verificado por teste do esquema (`PRAGMA table_info`).
- **Testes na fixture fictícia:** os bytes do `.db` gerado a partir dela não contêm o texto de endereço, os CEPs nem as coordenadas da fixture.
- **Amostra no arquivo real do 1º trimestre de 2025:** um teste (ignorado sem `SP156_XLSX`) monta uma amostra determinística de valores dessas cinco colunas e confere que nenhum aparece nos bytes do `.db` gerado. O teste detecta um valor plantado (verificado por mutação).
- **Totais no arquivo real:** 742.904 linhas e a contagem por status (`Finalizada` 715.139, `Aberto` 23.504, `Cancelada` 3.948, `Em Andamento` 313) batem com o que está em [docs/DATA.md](docs/DATA.md), no `.db` gerado.
- **Escrita segura:** o destino nunca é sobrescrito e uma falha não deixa `.tmp` nem `-journal` ao lado.

## O que NÃO está verificado

- **Outros trimestres e anos.** Tudo foi verificado em um único arquivo (1º trimestre de 2025): formato, colunas, valores e a amostra de privacidade.
- **O que a biblioteca de leitura decodifica em memória.** A proteção vale para o que é mapeado para o modelo e gravado no banco; o teste de amostra não diz nada sobre o que o Sylvan.Data.Excel lê na memória do processo.
- **A amostra de privacidade é uma amostra**, não uma verificação exaustiva: só valores com 7 ou mais caracteres e, no máximo, 200 por coluna. A cobertura para `Número` é praticamente nula (1 valor na amostra, porque quase todos os números de imóvel têm menos de 7 caracteres). **A proteção de `Número` vem do esquema** (a coluna não existe), não da amostra.
- A amostra só procura a forma UTF-8 exata dos valores.

## Limitações dos dados

- **Só 1 trimestre** (1º trimestre de 2025).
- **Os dados só registram solicitações feitas pela população.** Eles se referem exclusivamente às solicitações de serviços registradas pela população aos órgãos da Prefeitura. A Prefeitura pode executar um serviço sem ter recebido a solicitação correspondente (por exemplo, remover entulho que ninguém solicitou), então o volume de pedidos **não mede** a quantidade de serviços executados nem os problemas existentes na cidade. Fonte: nota de esclarecimento 3 da página [Dados Abertos – SP156](https://transparencia.prefeitura.sp.gov.br/dados-abertos-sp156/), consultada em 2026-11-12.
- **O status é um retrato de data desconhecida, não anterior a 2026-08-05 (data da última finalização no arquivo).** A data de extração não foi medida. Pedidos de janeiro a março de 2025 ainda abertos pelo menos 16 meses depois indicam que o tempo até finalizar tem **censura**: os pedidos ainda abertos não têm data de finalização para medir, então qualquer média de tempo até finalizar é viesada.
- **Sem deduplicação.** Os dados não têm ID de protocolo, a linha é a unidade e **47,65% das linhas são duplicatas exatas** de outra. A contagem é de linhas registradas, não de problemas ou pessoas distintas ([ADR 0003](docs/decisions/0003-nao-deduplicar-linhas.md)).
- Distrito e Prefeitura Operacional estão vazios em cerca de metade das linhas.

## Estrutura

```
src/Sp156.Ingest/            leitura do XLSX e gravação em SQLite
src/Sp156.Cli/               linha de comando (comando `ingerir`)
tests/Sp156.Ingest.Tests/    testes e fixture fictícia
tools/spike-xlsx/            medição descartável de bibliotecas (fora da solução)
docs/                        spec, dados, decisões (ADRs), diário de aprendizagem
```

## Desenvolvimento

```bash
dotnet build
dotnet test
```

Teste opcional contra o arquivo real (não versionado; ignorado quando a variável não existe):

```bash
SP156_XLSX=/caminho/arquivo-final-1-trim-25.xlsx dotnet test
```

Fluxo de trabalho: spec → testes vermelhos → implementação mínima. Ver [AGENTS.md](AGENTS.md) e [CONTRIBUTING.md](CONTRIBUTING.md).

## Licenças

- **Código:** [MIT](LICENSE).
- **Dados:** os dados do SP156 seguem a licença da fonte, **CC0 1.0 Universal**. A CC0 não resolve questões de dados pessoais: a conformidade com a LGPD deve ser validada por um advogado. Este projeto não afirma nada jurídico.
