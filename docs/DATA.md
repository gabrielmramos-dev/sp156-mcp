# Notas sobre os dados do SP156

Fonte: portal de dados abertos da Prefeitura de São Paulo, conjunto de dados do SP156: <https://dados.prefeitura.sp.gov.br/dataset/dados-do-sp156> (descrito na página [Dados Abertos – SP156](https://transparencia.prefeitura.sp.gov.br/dados-abertos-sp156/) do Portal da Transparência). Licença: **CC0 1.0 Universal** (dedicação ao domínio público). A CC0 não substitui a análise de dados pessoais: ver [ADR 0002](decisions/0002-privacidade-sem-enderecos.md).

Os números abaixo foram **medidos por inspeção** do arquivo `arquivo-final-1-trim-25.xlsx` (1º trimestre de 2025), em 2026-10-23. Valem para esse arquivo, não para a série inteira.

## Estrutura do arquivo

- Duas abas: `Solicitações` e `Satisfação`.
- Aba `Solicitações`, 15 colunas: Data de Abertura, Data de Finalização, Meio de Comunicação, Tema, Assunto, Serviço, Status, Órgão, Logradouro, Número, CEP, Latitude, Longitude, Prefeitura Operacional, Distrito.
- Aba `Satisfação`: as mesmas 15 colunas mais 4 perguntas da pesquisa. Só 883 linhas, todas `Finalizada`: amostra pequena e autosselecionada.
- **Sem ID de protocolo.**
- Formato interno: strings compartilhadas e datas como número serial. A aba `Solicitações` tem cerca de 518 MB de XML descompactado e 13 MB de strings compartilhadas.
- Tipos de célula nas 10 colunas usadas, medidos em 2026-11-12 percorrendo as 742.904 linhas: `Data de Abertura` é numérica em todas; `Data de Finalização` é numérica em 718.596 e texto (`''`) em 24.308; `Meio de Comunicação` tem 1 célula ausente; `Prefeitura Operacional` e `Distrito` têm célula **ausente** (não `''`) em 367.648 e 374.346 linhas; os demais textos são sempre texto. **Não há célula de erro, booleana nem de data** nessas colunas.

## Colunas que o projeto usa (10)

Data de Abertura, Data de Finalização, Meio de Comunicação, Tema, Assunto, Serviço, Status, Órgão, Prefeitura Operacional, Distrito.

**Descartadas na ingestão (5):** Logradouro, Número, CEP, Latitude, Longitude.

## Armadilhas confirmadas

| Achado | Consequência |
|---|---|
| A aba declara 1.048.576 linhas; só **742.904** têm dado | Contar linhas sem filtrar vazias dá o número errado |
| **47,65%** das linhas reais são duplicatas exatas | A linha não é uma solicitação única; não deduplicar |
| `Data de Finalização` ausente vem como string vazia `''`, não como célula vazia: 24.308 linhas (23.504 `Aberto`, 313 `Em Andamento`, **491 `Finalizada`**) | Tratar `''` como nulo; 491 pedidos finalizados não têm data |
| `Meio de Comunicação` ausente em 1 linha real | O campo é anulável |
| Status: `Finalizada` 715.139, `Aberto` 23.504, `Cancelada` 3.948, `Em Andamento` 313 | Valores diferem da documentação oficial |
| Meio: CRM, PORTAL, MOBILE, INTEGRACAO, CHATBOT, RESOLUCAO | Diferem da documentação oficial |
| Status é um retrato de ago/2026 (última finalização: 2026-08-05) | Pedidos de jan–mar/2025 ainda abertos quase 19 meses depois: tempo até finalizar tem **censura** |
| Distrito e Prefeitura Operacional vazios em ~50% das linhas reais | Toda resposta por distrito deve informar quantas linhas ficaram sem distrito |
| Geografia depende do tema (Rua e bairro, Lixo e limpeza, Urgência ~100%; Cultura/esporte, Finanças, Saúde 0–2%) | Perguntas geográficas só valem para parte dos temas |
| Latitude/Longitude vêm como texto; há 2.517 linhas (0, 0) e ~2.570 fora de SP capital | (Irrelevante hoje: as colunas são descartadas) |
| Espaço duplo no meio do texto em 13 valores (2 em `Assunto`, 11 em `Serviço`); sem variação de acento nem de caixa em nenhuma coluna | O texto guardado recebe só NFC e trim; o espaço duplo é mantido. `TextoNormalizado.Chave` existe para busca. Normalização mais agressiva adiada por falta de evidência (1 trimestre medido) |

## Limitações

- **Os dados só registram solicitações feitas pela população.** Eles se referem exclusivamente às solicitações de serviços registradas pela população aos órgãos da Prefeitura. A Prefeitura pode executar um serviço sem ter recebido a solicitação correspondente (por exemplo, remover entulho que ninguém solicitou), então o volume de pedidos **não mede** a quantidade de serviços executados nem os problemas existentes na cidade. Fonte: nota de esclarecimento 3 da página [Dados Abertos – SP156](https://transparencia.prefeitura.sp.gov.br/dados-abertos-sp156/), consultada em 2026-11-12.
- Valem também as armadilhas acima: só 1 trimestre verificado, a linha não é uma solicitação única e o status é um retrato de ago/2026.

## Divergências entre a documentação oficial e o arquivo real

A documentação (glossário e página do conjunto de dados) descreve colunas e valores que **não** coincidem com o arquivo: por exemplo, `Subprefeitura` (real: `Prefeitura Operacional`), status `Indeferido`/`Aberta` (inexistentes no arquivo) e ausência de latitude/longitude na aba `Solicitações` (existem). Regra do projeto: **validar pelo conteúdo do arquivo, não pela documentação.**

## Verificado e ainda não verificado

**Verificado, só neste arquivo (1º trimestre de 2025):** a ingestão completa reproduz os totais acima (linhas, status, `Data de Finalização` nula, `Meio de Comunicação` nulo), e os tipos de célula das 10 colunas usadas são os descritos em "Estrutura do arquivo".

**Ainda não verificado:**

- Os demais trimestres e anos: formato, tamanho, mudanças de colunas e variação de acento, caixa ou espaços nos textos. Checar antes de qualquer normalização mais agressiva do dado guardado.
- Se algum trimestre excede o limite de linhas do XLSX (hipótese não testada).
- Comparabilidade entre o sistema de 2012–2014 e o de 2015 em diante.
- O que a biblioteca de leitura decodifica em memória das colunas de endereço (a proteção vale para o que é mapeado e gravado; ver ADR 0002).
- Cobertura da amostra de privacidade para `Número`: praticamente nula (1 valor com 7 ou mais caracteres). A proteção dessa coluna vem do esquema, que não a tem.
