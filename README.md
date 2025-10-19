# Solution CsvProcessorWithOllama

Este � um exemplo simples do uso da Inteligencia Artificial para processar um arquivo CSV

A pretens�o � ser generico quanto ao arquivo sendo que as colunas propostas tem que existir nesta
mesma ordem do arquivo XML, caso queira incrementar sugiro inicar novas colunas apartir do index=6

A estrutura � de uma minimal API n�o separei em pastas para ficar f�cil o entendimento. 
Neste caso o TO-DO no final tem sugest�es de melorias para que voce estude e pratique.

O que o projeto faz � ler um arquivo .CSV cujo nome e path vai estar no appsettings.
ler o cabe�alho (colunas) de um arquvio headers.xml

Caso voce n�o tenha um arquivo eu deixei na pasta PHYTON um script pra criar o arquivo .CSV
(totalmente freee !! - eu estou muito bonzinho !!)

Usamos a Microsoft.Extension.IA e o projeto Ollama, pagina do dosc -> https://docs.ollama.com/
sugiro antes mesmo de tentar entender o projeto leia, baixe e instale o Ollama

Existe um arquivo Index.html com um usuario, data e texto para o chat (100 palavras)
O que ele faz, voce pode perguntar a IA dados sobre o aquivo:

exemplo: "Some somente os produtos com 'Codigo' = '47' "
exemplo 2: "Liste os produtos Codigo ou (primeira coluna) cujo valor calculado seja menor que 100"
exemplo 3: "Agrupe pelo produto Codigo ou (primeira coluna) todos que o valor Status ou (quinta coluna) seja 'N'"

Para ficar mais legal usei o MONGODB para gravar as execu��es primarias do projeto.

## Visual Studio

Use o Visual Studio 2022 - instale o .NET 9

## Pacotes NUGETS

Instale:
    - CsvHelper Version="33.1.0"
    - Microsoft.Extensions.Configuration.Json" Version="9.0.10"
    - Microsoft.Extensions.DependencyInjection" Version="9.0.10"
    - Microsoft.Extensions.Hosting" Version="9.0.10"
    - Microsoft.Extensions.Http" Version="9.0.10"
    - Microsoft.VisualStudio.Azure.Containers.Tools.Targets" Version="1.22.1"
    - MongoDB.Driver" Version="3.5.0"
    - System.Drawing.Common" Version="9.0.10"
    - System.Text.Json" Version="9.0.10"
    - System.Linq.Dynamic.Core Version="1.6.9"

## Instru��es detalhadas para voce criar seu projeto

# Crie uma nova pasta e cole este conte�do nos arquivos:
appsettings.json (conforme acima)
headers.xml
sample.csv na pasta especificada por InputFolder (ou crie a pasta de entrada e coloque o sample.csv l�)
Program.cs (o c�digo do programa C# deste arquivo)
wwwroot/index.html (o conte�do HTML acima)

Execute o servidor ollama ou configure o endpoint do Ollama no appsettings.json. Se voc� n�o tiver o Ollama, 
        ainda pode testar a execu��o e a inser��o no Mongo localmente comentando a chamada de IA.

Execute o projeto (dotnet run) e abra http://localhost:5000 (ou a porta exibida). Use a interface de chat.

Observa��es:
A an�lise do CSV aqui � simplificada (sem v�rgulas entre aspas). Para uso em produ��o, substitua por CsvHelper.
A integra��o com IA chama o endpoint HTTP /api/chat do Ollama. Ajuste caso sua vers�o do Ollama seja diferente.
A gera��o de gr�ficos usa System.Drawing; em sistemas operacionais n�o Windows, pode ser necess�rio instalar depend�ncias adicionais ou usar uma biblioteca de plotagem como ScottPlot.

## How to Run

1. **Prerequisitos**:
   - .NET SDK 9.0

2. **Build and Run**:
   dotnet build
   dotnet run

## TO-DO

- Voce pode criar uma interface e servi�os para algumas fun��es 

- voce pode e deve modificar o projeto para uma arquitetura mais CLEAN code.

- voce deve usar sua criatividade e criar uma planilha mais especifica de calculo, 
  Sugest�es: Fluxo de caixa e ou Deprecia��o de materiais.

ESPERO QUE VOCE APRENDA ALGUMA COISA NISTO. 

UM FORTE ABRA�O.

WELLINGTON GUIMARAES PIMENTA - AUTOR DA Baga�a !!

"Porque DEUS amou o mundo de tal maneira que deu o seu UNICO filho para nos salvar." Jo�o 3:16

Jesus � o unico caminho para a vida ETERNA. 