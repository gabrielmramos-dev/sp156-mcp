# Notas sobre os dados do SP156

Fonte: portal de dados abertos da Prefeitura de São Paulo. Licença: **CC0 1.0 Universal** (dedicação ao domínio público). A CC0 não substitui a análise de dados pessoais: ver [ADR 0002](decisions/0002-privacidade-sem-enderecos.md).

Os números abaixo foram **medidos por inspeção** do arquivo `arquivo-final-1-trim-25.xlsx` (1º trimestre de 2025), em 2026-10-23. Valem para esse arquivo, não para a série inteira.

## Estrutura do arquivo

- Duas abas: `Solicitações` e `Satisfação`.
- Aba `Solicitações`, 15 colunas: Data de Abertura, Data de Finalização, Meio de Comunicação, Tema, Assunto, Serviço, Status, Órgão, Logradouro, Número, CEP, Latitude, Longitude, Prefeitura Operacional, Distrito.
- Aba `Satisfação`: as mesmas 15 colunas mais 4 perguntas da pesquisa. Só 883 linhas, todas `Finalizada`: amostra pequena e autosselecionada.
- **Sem ID de protocolo.**
- Formato interno: strings compartilhadas e datas como número serial. A aba `Solicitações` tem cerca de 518 MB de XML descompactado e 13 MB de strings compartilhadas.

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
| `Assunto` com espaço duplo; `Órgão` com e sem acento (`INTEGRAÇÃO-`/`INTEGRACAO-`) | Normalizar na ingestão |

## Divergências entre a documentação oficial e o arquivo real

A documentação (glossário e página do conjunto de dados) descreve colunas e valores que **não** coincidem com o arquivo: por exemplo, `Subprefeitura` (real: `Prefeitura Operacional`), status `Indeferido`/`Aberta` (inexistentes no arquivo) e ausência de latitude/longitude na aba `Solicitações` (existem). Regra do projeto: **validar pelo conteúdo do arquivo, não pela documentação.**

## O que ainda não foi verificado

- Os demais trimestres e anos (formato, tamanho, mudanças de colunas).
- Se algum trimestre excede o limite de linhas do XLSX (hipótese não testada).
- Comparabilidade entre o sistema de 2012–2014 e o de 2015 em diante.
