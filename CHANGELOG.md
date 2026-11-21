# Mudanças

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
