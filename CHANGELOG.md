# Mudanças

## 2.0.3

Versão de testes. Ainda não é o programa pronto.

A janela saiu do verde e do painel arredondado. O fundo é grafite, o texto é claro, o botão e a aba selecionada usam azul de aço, e os cantos são retos. A logo acompanhou: o raio continua, sem o verde.

O programa é para muita gente, não para um PC só. Parar o SQL Server, ou qualquer serviço de um programa que só existe numa máquina, saiu de vez. A lista que fechava Spotify, Epic, EA, Medal, Lossless Scaling e o overlay da NVIDIA também saiu. Isso era o que estava aberto num computador de teste.

Olhei o EXM, o winutil do Chris Titus e os pacotes de debloat que se repetem. Entrou o que eles aplicam em Windows 10 e 11 sem escolher o hardware da pessoa:

- Preparar para jogar só fecha papel de parede animado, se estiver aberto, e libera memória no modo suave. Wallpaper Engine, Lively e parecidos. Launcher, Spotify, Discord, Steam, navegador e o jogo ficam.
- Tirar papel de parede, na aba Aplicativos, faz a mesma coisa na inicialização. O resto a pessoa desliga na lista, um por um.
- A GPU acelerada saiu do 1 clique. Em umas placas a latência cai, em outras a imagem engasga. Continua na aba Jogo, desmarcada.
- A energia passa a dar prioridade ao programa da frente. É a opção Programas das propriedades de desempenho do Windows.
- Telemetria também desliga o histórico de atividade e a localização do Windows. A atualização continua.
- A interface deixa de redesenhar a janela o tempo todo enquanto ela é arrastada. No Windows 11, a barra de tarefas ganha Encerrar tarefa.
- SysMain, na aba Sistema, só desliga no Windows 10, e só se a pessoa marcar. No Windows 11 ele fica ligado.

Ficou de fora o que esses aplicativos vendem a mais e que quebra PC de outra pessoa: BIOS, timer, fila do mouse, Nagle, plano Ultimate, desligar o Windows Update e desligar a otimização de tela cheia. Monitor e taxa da tela também ficam quietos.

A integridade de memória continua ligada. O Valorant precisa dela.

## 2.0.2

Versão de testes. Ainda não é o programa pronto.

Olhei o que os otimizadores grandes gravam de fato: o winutil do Chris Titus, o guia de latência que publica a curva do MarkC para cada escala de tela, e os pacotes de "mira" que enchem o registro. Entrou só o que muda o ponteiro ou o peso do Windows.

No mouse, além de desligar a aceleração, a curva fica reta na escala desta tela, de 100% a 350%. É a mesma curva do MarkC. A velocidade do ponteiro vai para a marca do meio. Se a escala for outra, a curva não é trocada. O rastro do ponteiro e o salto automático para o botão padrão saem junto.

O perfil de mídia dos jogos fica com prioridade alta, do jeito que esses guias completam a chave que o programa já gravava. Na rede com fio, a economia de energia da placa (EEE) desliga junto com a suspensão do USB. A rede pode piscar uma vez. Os serviços do Windows passam a caber em menos processos, usando a RAM deste PC, como o winutil faz.

Ficou de fora de propósito: fila do mouse em 16 ou 20, prioridade realtime no driver, Nagle, plano Ultimate, desligar a otimização de tela cheia e timer. Isso aparece em vários repositórios e atrapalha mais do que ajuda.

A limpeza de memória do .bat antigo voltou para a aba Limpeza. Tem o modo suave, o completo, e o atalho CTRL+ALT+R, que roda sem abrir a janela. Não entra no 1 clique. Jogo, Discord, Steam e Valorant ficam de fora dessa limpeza.

## 2.0.1

Quem acabava de instalar via as opções já ligadas. Não era ajuste nenhum gravado no Windows: o programa abria com tudo marcado. Agora começa desmarcado, nas abas Jogo, Sistema e Limpeza, e também na lista de apps. O 1 clique continua aplicando o pacote recomendado. Quem quiser a lista inteira de apps usa "Marcar instalados".

O 1 clique parava com a frase "tentativa de executar uma operação não autorizada" e o resto do pacote não entrava. O Windows recusa algumas gravações mesmo com o programa como administrador. A que mais faz isso é a do menu clássico do botão direito, no Windows 11. O programa mostrava a frase crua e desistia. Agora ele pula a chave recusada, segue com o resto e avisa no final o que ficou de fora.

Os serviços da Xbox estavam nesse pacote, indo para início manual. Isso atrapalha login, Game Pass e jogo online, e não muda FPS. Saíram. Telemetria, anúncio e Copilot continuam.

Se o programa abrir sem ser administrador, o aviso pede para fechar e abrir de novo, aceitando a janela do Windows.

## 2.0.0

Primeira versão com instalador. O setup baixa só o NashBoost.exe.
