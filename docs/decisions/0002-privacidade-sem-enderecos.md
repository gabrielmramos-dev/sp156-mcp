---
status: accepted
date: 2026-10-23
---

# Não ler nem gravar endereço e coordenadas

## Contexto e problema

O arquivo do SP156 traz logradouro, número, CEP e coordenadas exatas junto com o tipo de serviço. A licença CC0 permite o reuso dos dados, mas não resolve a questão de dados pessoais: um endereço associado a um tipo de pedido pode identificar uma pessoa. A decisão não é um parecer jurídico.

## Fatores de decisão

* As perguntas planejadas são agregadas (por serviço, distrito, período).
* Evitar vazamento por construção é mais seguro do que por filtro.

## Opções consideradas

* Expor linhas completas, com endereço
* Expor linhas sem endereço, mas manter o dado no banco
* Descartar as colunas na ingestão e expor só agregados por distrito

## Resultado da decisão

Escolhida a terceira. Logradouro, Número, CEP, Latitude e Longitude **não são mapeados para o modelo nem gravados**. A biblioteca de leitura precisa parsear as células, mas o dado não chega ao modelo nem ao banco, então nenhuma ferramenta futura consegue vazá-lo.

### Consequências

* Bom: a garantia é estrutural e verificável (nenhuma coluna de endereço no esquema).
* Ruim: sem mapa e sem consultas por ponto ou raio.

### Confirmação

Testes: (1) a tabela SQLite não contém coluna de endereço nem coordenada; (2) os bytes do `.db` gerado a partir da fixture não contêm o texto de endereço nem as coordenadas dela.

## Mais informações

Reavaliar se houver demanda por consultas geográficas, com nova análise de privacidade.
