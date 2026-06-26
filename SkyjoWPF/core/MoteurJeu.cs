using System;
using System.Collections.Generic;
using System.Linq;

namespace SkyjoWPF.Core
{
    public class MoteurJeu
    {
        public List<Carte> Pioche { get; private set; }
        public Stack<Carte> Defausse { get; private set; }
        public Carte[] GrilleJoueur { get; private set; }

        public MoteurJeu()
        {
            Pioche = new List<Carte>();
            Defausse = new Stack<Carte>();
            GrilleJoueur = new Carte[12];
            InitialiserPartie();
        }

        private void InitialiserPartie()
        {
            AjouterCartes(-2, 5);
            AjouterCartes(-1, 10);
            AjouterCartes(0, 15);
            for (int i = 1; i <= 12; i++) AjouterCartes(i, 10);

            MelangerPioche();

            for (int i = 0; i < 12; i++)
            {
                GrilleJoueur[i] = TirerCarte();
            }

            Carte premiereDefausse = TirerCarte();
            premiereDefausse.EstVisible = true;
            Defausse.Push(premiereDefausse);
        }

        private void AjouterCartes(int valeur, int quantite)
        {
            for (int i = 0; i < quantite; i++)
                Pioche.Add(new Carte(valeur));
        }

        private void MelangerPioche()
        {
            Random rng = new Random();
            int n = Pioche.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                Carte value = Pioche[k];
                Pioche[k] = Pioche[n];
                Pioche[n] = value;
            }
        }

        public Carte TirerCarte()
        {
            if (Pioche.Count == 0) return null;
            Carte c = Pioche.First();
            Pioche.RemoveAt(0);
            return c;
        }
    }
}