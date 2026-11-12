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

(3) Teste com o arquivo real (`FactArquivoReal`, ignorado sem `SP156_XLSX`): lê do XLSX, em streaming, só as colunas Logradouro, Número, CEP, Latitude e Longitude e monta uma **amostra** determinística (um valor candidato a cada 4.000, não vazio e com 7 ou mais caracteres, até 200 distintos por coluna). Em seguida procura cada valor, como bytes UTF-8, no `.db` inteiro, lido em blocos com sobreposição, e afirma que nenhum aparece.

* **O que isto mostra:** nenhum valor dessa amostra, no arquivo do 1º trimestre de 2025, aparece nos bytes do `.db` gerado. O teste detecta um valor plantado: com uma mutação temporária que gravou um logradouro da amostra no assunto da primeira linha, ele falhou (`Logradouro: 1 de 94`), e voltou a passar ao remover a mutação.
* **O que isto NÃO mostra:** (a) é uma amostra, não uma verificação exaustiva: na execução de 12/11/2026 foram 94 logradouros, 92 CEPs, 93 latitudes, 91 longitudes e 1 número; valores com menos de 7 caracteres (a maioria dos números de imóvel e o `0` das coordenadas) ficam de fora, porque casariam por acaso; (b) vale para 1 trimestre, e os demais não foram verificados; (c) não diz nada sobre o que a biblioteca de leitura decodifica na memória do processo, nem sobre arquivos temporários do sistema; (d) só procura a forma UTF-8 exata dos valores, então uma variante transformada (por exemplo, com outra caixa ou formatação) não seria achada.

## Mais informações

Reavaliar se houver demanda por consultas geográficas, com nova análise de privacidade.
