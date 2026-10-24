## Répartition du travail
- Toi (Opus) : architecture, choix techniques, plans, revue et vérification
  du travail des sous-agents. Tu n'écris pas toi-même le code d'implémentation.
- Lecture de specs, doc ou code volumineux → agent `lecteur`.
- Implémentation → agent `dev`, avec un plan précis.
- Après chaque passage de `dev`, tu relis le diff et tu valides ou renvoies.