🎴 Skyjo PC — Version WPF
Développé par Gira Studio

Skyjo PC est une adaptation informatique du célèbre jeu de cartes Skyjo, réalisée en C# avec WPF.
Le but : proposer une version jouable sur PC, fidèle aux règles originales, avec une interface simple et agréable.

✨ Fonctionnalités actuelles
🃏 Gestion des cartes (pioche, défausse, grille du joueur)

🔄 Logique de tour (piocher, échanger, retourner une carte)

📊 Calcul du score selon les règles officielles

🎨 Interface WPF en cours de développement

🧪 Prototype jouable permettant de tester les mécaniques de base

Le projet est encore en développement : certaines fonctionnalités sont en cours d’implémentation ou de test.

🚧 État du projet
Ce projet est développé en solo, sur le temps libre.
Les mises à jour arrivent au fur et à mesure, selon l’avancement et les disponibilités.

Actuellement :

✔️ Base du jeu fonctionnelle

✔️ Interface WPF initiale

🔧 Amélioration de l’UI en cours

🔜 Ajout d’un mode multijoueur local

🔜 Effets visuels et animations

🛠️ Technologies utilisées
C# (.NET Framework / WPF)

XAML pour l’interface

Visual Studio 2019


📦 Installation & Exécution
Clone le dépôt :

bash
git clone https://github.com/giragame8/SkyjoWPF.git
Ouvre le projet dans Visual Studio 2019

Lance l’application avec F5

Aucune installation supplémentaire n’est requise.

📁 Structure du projet
Code
SkyjoWPF/
 ├── Views/           # Fenêtres et pages WPF
 ├── ViewModels/      # Logique de présentation (en cours)
 ├── Models/          # Objets du jeu (cartes, joueur, pioche…)
 ├── Assets/          # Images, ressources
 ├── App.xaml         # Configuration globale WPF
 └── MainWindow.xaml  # Interface principale
🧩 Règles du jeu (résumé)
Chaque joueur possède une grille de 12 cartes face cachée

Au début, il retourne 2 cartes

À son tour, il peut :

Piocher une carte inconnue

Prendre la carte visible de la défausse

Il peut ensuite :

Remplacer une carte de sa grille

Ou défausser et retourner une carte

Si une colonne contient 3 cartes identiques, elle disparaît

La partie se termine quand un joueur a révélé toutes ses cartes

Le joueur avec le plus petit score gagne

🧑‍💻 Auteur
Gira Studio  
Développement solo — Projet hobby / apprentissage WPF

📜 Licence
Projet personnel — utilisation libre pour consultation et apprentissage.
Toute redistribution commerciale est interdite.
