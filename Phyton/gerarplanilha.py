import pandas as pd
import random

# Script By Wellington G Pimenta
# use como modelo para gerar a planilha de dados
# use sua criatividade e alter o projeto no visual studio 
# para calculos e consultas nesta planilha pela IA


# Configurações
total_linhas = 25000
codigos_por_produto = 25
num_produtos = total_linhas // codigos_por_produto

# Verificar se o número de linhas é divisível por 25
if total_linhas % codigos_por_produto != 0:
    print(f"Aviso: {total_linhas} não é divisível por {codigos_por_produto}")
    num_produtos = total_linhas // codigos_por_produto
    total_linhas = num_produtos * codigos_por_produto
    print(f"Serão criadas {total_linhas} linhas")

# Gerar dados
dados = []

# Gerar códigos únicos começando de 1000
codigos_base = list(range(1000, 1000 + num_produtos))

# Garantir taxas únicas para todas as linhas
todas_taxas = random.sample(range(0, 100001), total_linhas)  # 0 a 100000 para ter mais variação
todas_taxas = [taxa / 1000 for taxa in todas_taxas]  # Converter para 0.000 a 100.000

taxa_index = 0

for codigo in codigos_base:
    nome = f"Produto numero {codigo}"
    
    # Gerar 25 linhas para cada código
    for i in range(codigos_por_produto):
        quantidade = codigo / 4
        taxa = todas_taxas[taxa_index]
        taxa_index += 1
        
        # Status: primeiras 20 linhas "S", últimas 5 "N"
        status = "S" if i < 20 else "N"
        
        dados.append({
            "Codigo": codigo,
            "Nome": nome,
            "Quantidade": quantidade,
            "Taxa": taxa,
            "Status": status
        })

# Criar DataFrame
df = pd.DataFrame(dados)

# Ordenar por código para melhor organização
df = df.sort_values('Codigo').reset_index(drop=True)

# Salvar como CSV
df.to_csv('VendaseTaxas.csv', index=False, encoding='utf-8')

print(f"Planilha 'VendaseTaxas.csv' criada com sucesso!")
print(f"Total de linhas: {len(df)}")
print(f"Total de produtos únicos: {num_produtos}")
print(f"Linhas por produto: {codigos_por_produto}")
print("\nPrimeiras 10 linhas:")
print(df.head(10))
print("\nÚltimas 10 linhas:")
print(df.tail(10))
print(f"\nVerificação - Status por código:")
verificacao = df.groupby('Codigo')['Status'].value_counts().head(10)
print(verificacao)
