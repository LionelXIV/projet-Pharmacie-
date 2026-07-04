# Module d'import produits — Guide utilisateur et développeur

## 1. Présentation du module

Le module d'import permet de charger un fichier Excel contenant le catalogue et les stocks initiaux d'une pharmacie, de détecter les anomalies avant toute écriture en base, puis de créer ou mettre à jour les produits et les lots de manière contrôlée.

### Workflow complet

1. **Upload du fichier Excel**  
   L'utilisateur (administrateur ou pharmacien) téléverse un fichier `.xlsx` depuis l'écran **Import produits**. Le système lit le fichier, valide chaque ligne et enregistre un **lot d'import** temporaire (lignes + anomalies) sans modifier le catalogue.

2. **Prévisualisation**  
   L'écran de prévisualisation affiche un résumé : nombre de créations produit, mises à jour de prix, nouveaux lots, lignes ignorées et anomalies. Chaque ligne indique l'action prévue et les alertes associées. Aucune donnée catalogue n'est encore définitive à ce stade.

3. **Analyse et traitement des anomalies**  
   Si des anomalies **bloquantes** subsistent, l'utilisateur est orienté vers l'écran **Anomalies**. Pour chaque ligne concernée, il peut **ignorer** la ligne ou **forcer l'import** (avec saisie d'un PPH de remplacement si le prix de vente est nul). Les avertissements non bloquants sont visibles en prévisualisation mais n'empêchent pas la confirmation.

4. **Confirmation de l'import**  
   Une fois toutes les anomalies bloquantes résolues, l'utilisateur confirme l'import depuis la prévisualisation. L'opération est **transactionnelle** : en cas d'erreur, rien n'est conservé partiellement.

5. **Rapport final**  
   L'écran **Résultat** récapitule le lot confirmé : produits créés, lots de stock créés, produits mis à jour, anomalies traitées, etc.

---

## 2. Format du fichier Excel attendu

Le fichier doit être au format **`.xlsx`**. L'en-tête des colonnes peut se trouver dans les **100 premières lignes** de la feuille ; l'ordre des colonnes est libre. Les noms de colonnes sont reconnus sans tenir compte de la casse.

| Colonne Excel | Signification métier | Champ `Product` correspondant |
|---------------|-------------------|-------------------------------|
| **CIP** | Code Identifiant de Présentation du produit. Identifiant principal utilisé pour reconnaître un article déjà présent dans le catalogue. | `Product.Cip` |
| **REFHA** | Référence HA (optionnelle). Complément d'identification fournisseur ou référentiel. | `Product.Refha` |
| **LIBELLE** | Dénomination commerciale ou libellé du produit tel qu'il apparaît sur la facture ou l'état de stock. | `Product.CommercialName` |
| **QTEFACT** | Quantité associée à la ligne d'import. Utilisée pour créer un **lot de stock** lorsque la quantité est strictement positive. | *(pas de champ direct sur `Product` — alimente les lots via le module stock)* |
| **PX_FAB** | Prix fabricant / prix d'achat de référence indiqué dans le fichier source. | `Product.ReferencePurchasePrice` |
| **PPH** | Prix pharmacie / prix de vente réglementé de référence indiqué dans le fichier source. | `Product.RegulatedSalePrice` |

**Colonnes obligatoires :** CIP, LIBELLE, QTEFACT, PX_FAB, PPH.  
**Colonne optionnelle :** REFHA.

Lors de la **création** d'un produit, le système renseigne également `Product.PurchasePrice` et `Product.SalePrice` à partir de PX_FAB et PPH, en plus des champs de référence ci-dessus. Lors de la **mise à jour** d'un produit existant, seuls `ReferencePurchasePrice` et `RegulatedSalePrice` sont actualisés.

---

## 3. Gestion du type de produit

Tous les produits **nouvellement créés** par l'import reçoivent systématiquement :

```text
ProductType = Inconnu
```

Ce comportement est **volontaire**. Le fichier Excel fourni ne contient pas d'information permettant de distinguer de manière fiable un **médicament** d'un article de **parapharmacie**. Attribuer automatiquement l'un ou l'autre exposerait la pharmacie à des erreurs de classification réglementaire ou de gestion.

**Après l'import**, la classification doit être effectuée **manuellement** via l'écran **Products** (catalogue produits), ligne par ligne ou par lots de corrections.

Il ne s'agit pas d'un défaut du module, mais d'une **décision métier assumée** : l'import initialise le catalogue ; la pharmacie conserve la responsabilité de la typologie des articles.

---

## 4. Anomalies détectées automatiquement

Le système distingue deux niveaux :

- **Bloquante** : la ligne ne sera pas importée tant que l'utilisateur n'a pas pris une décision explicite (ignorer ou forcer).
- **Avertissement** : signal informatif ; la confirmation reste possible sans action obligatoire.

### CIP manquant

| | |
|---|---|
| **Signification** | La cellule CIP est vide ou ne contient pas de valeur exploitable. |
| **Pourquoi c'est signalé** | Sans CIP, le produit ne peut pas être identifié ni rapproché du catalogue existant. |
| **Que faire** | Corriger le fichier source et relancer l'import, **ou** ignorer la ligne depuis l'écran Anomalies si elle doit être écartée. |

*Sévérité : bloquante.*

### Libellé vide

| | |
|---|---|
| **Signification** | La colonne LIBELLE est vide. |
| **Pourquoi c'est signalé** | Le nom commercial est indispensable pour identifier le produit en pharmacie. |
| **Que faire** | Compléter le libellé dans Excel et réimporter, **ou** ignorer la ligne. |

*Sévérité : bloquante.*

### PPH nul

| | |
|---|---|
| **Signification** | La colonne PPH est absente, vide ou égale à zéro. |
| **Pourquoi c'est signalé** | Un prix de vente de référence nul empêche une exploitation fiable du catalogue (tarification, contrôles). |
| **Que faire** | Corriger le PPH dans le fichier, **ou** forcer l'import en saisissant un **PPH de remplacement** strictement positif sur l'écran Anomalies. |

*Sévérité : bloquante.*

### PPH inférieur ou égal au prix fabricant

| | |
|---|---|
| **Signification** | Le PPH est renseigné mais inférieur ou égal au PX_FAB de la même ligne. |
| **Pourquoi c'est signalé** | Cette situation est inhabituelle en pharmacie (marge négative ou nulle) et mérite une vérification. |
| **Que faire** | Vérifier les montants dans le fichier source. L'import peut être confirmé malgré cet avertissement ; aucune action obligatoire sur l'écran Anomalies. |

*Sévérité : avertissement (non bloquant).*

### Quantité négative

| | |
|---|---|
| **Signification** | QTEFACT est strictement inférieur à zéro. |
| **Pourquoi c'est signalé** | Une quantité négative n'a pas de sens pour un stock physique. |
| **Que faire** | Corriger la quantité dans Excel. L'avertissement n'empêche pas la confirmation si la valeur est acceptée telle quelle après vérification. |

*Sévérité : avertissement (non bloquant).*

### CIP dupliqué

| | |
|---|---|
| **Signification** | Le même CIP apparaît sur plusieurs lignes du fichier. |
| **Pourquoi c'est signalé** | Pour attirer l'attention sur un regroupement qui peut être volontaire (plusieurs arrivages) ou involontaire (doublon de saisie). |
| **Que faire** | Vérifier que chaque ligne correspond bien à un mouvement attendu. Si les libellés sont cohérents, aucune action n'est requise : voir section 5. |

*Sévérité : avertissement (non bloquant).*

### Même CIP avec libellés différents

| | |
|---|---|
| **Signification** | Plusieurs lignes partagent le même CIP mais avec des libellés qui diffèrent (après normalisation des espaces et de la casse). |
| **Pourquoi c'est signalé** | Un même code CIP devrait désigner un seul produit ; des libellés divergents indiquent une incohérence de données source. |
| **Que faire** | Harmoniser les libellés dans Excel et relancer l'import, **ou** ignorer les lignes concernées, **ou** forcer après vérification métier que le CIP est correct. |

*Sévérité : bloquante.*

---

## 5. Gestion des CIP dupliqués

Plusieurs lignes portant le **même CIP** ne sont **pas** considérées comme une erreur en soi.

Dans la pratique pharmaceutique, cela peut représenter :

- plusieurs **arrivages** successifs du même produit ;
- plusieurs **lots** distincts issus de livraisons différentes ;
- une même référence facturée en plusieurs fois dans l'état de stock exporté.

Le système traite ces lignes comme des **lots distincts** :

- la **première occurrence** d'un CIP inconnu dans le catalogue déclenche une **création produit** ;
- les occurrences suivantes du même CIP (dans le même fichier) déclenchent un **nouveau lot** pour ce produit ;
- si le CIP existe déjà en base, chaque ligne avec quantité positive crée un **nouveau lot**, sinon seule une **mise à jour des prix de référence** est effectuée.

Seule l'**incohérence de libellé** pour un même CIP est traitée comme **bloquante** (voir section 4). Le simple fait d'avoir le CIP en double génère uniquement un **avertissement**.

---

## 6. Limites connues de cette première version

| Limite | Détail |
|--------|--------|
| **Dates d'expiration provisoires** | Le fichier Excel ne fournit pas de date de péremption. Lors de la confirmation, chaque lot créé reçoit une date d'expiration provisoire fixée à **aujourd'hui + 2 ans**. |
| **Correction manuelle des lots** | Ces dates doivent être **corrigées** par la pharmacie via le module **Lots** avant une exploitation fiable (ventes, alertes péremption, conformité). |
| **Pas de classification automatique** | Aucun produit importé n'est classé Médicament ou Parapharmacie ; tous restent `Inconnu` jusqu'à action manuelle. |
| **Volume réel non validé** | Le module a été testé techniquement (build, tests unitaires, migrations), mais **n'a pas encore été validé** sur le volume réel de production de la pharmacie (taille du fichier, performance, exhaustivité des cas métier). |
| **Catégorie et fournisseur par défaut** | Les produits créés sont rattachés à une catégorie « À catégoriser » et un fournisseur « Fournisseur non précisé », à affiner manuellement après import. |
| **Format Excel** | Seuls les fichiers `.xlsx` sont acceptés (pas de `.xls` ni CSV). |

---

## 7. Questions ouvertes

Les points ci-dessous reposent sur des **hypothèses** issues de l'analyse du fichier fourni. Ils doivent être **confirmés officiellement avec la pharmacie** avant une mise en production.

### QTEFACT

Le système suppose actuellement que **QTEFACT représente une quantité physique de stock disponible** à enregistrer en lot lorsque la valeur est strictement positive.

Il reste à confirmer avec la pharmacie si cette colonne correspond réellement à :

- un **stock physique réel** présent en officine ;
- une **quantité facturée** sur un document comptable ;
- une **quantité commandée** non encore reçue ;
- ou une **autre notion métier** propre à l'outil d'export source.

Tant que cette question n'est pas tranchée, les quantités importées et les lots créés doivent être **contrôlés manuellement** après l'import.

### PX_FAB et PPH

L'implémentation actuelle retient l'interprétation suivante :

| Colonne | Interprétation retenue | Champ système |
|---------|------------------------|---------------|
| **PX_FAB** | Prix fabricant / prix d'achat de référence | `Product.ReferencePurchasePrice` |
| **PPH** | Prix pharmacie / prix de vente réglementé de référence | `Product.RegulatedSalePrice` |

Cette lecture provient de l'analyse du fichier d'exemple et des libellés métier habituels, mais elle **n'a pas été validée formellement** par la pharmacie. Une interprétation différente (par exemple PPH hors taxes, prix public maximum, etc.) impliquerait d'adapter le mapping ou les contrôles.

---

## 8. Conclusion

Le module d'import constitue un outil de **chargement initial du catalogue** et des stocks associés, avec un filet de sécurité sur les données :

- les **anomalies sont détectées et contrôlées** avant toute écriture définitive ;
- les opérations de confirmation sont **transactionnelles** ;
- les décisions sensibles (type de produit, dates de péremption, interprétation des colonnes) restent sous la responsabilité de la pharmacie.

La **validation métier finale avec la pharmacie** — sur un fichier réel, en conditions d'exploitation — reste **indispensable** avant toute mise en production.

---

*Document généré pour le module d'import produits — version initiale post-correction Step9_RemoveResolvedActionDefault.*
