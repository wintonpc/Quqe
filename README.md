- general workflow
  - select ProtoRun from database
  - store Run with chosen ProtoChromosome
  - store generation 0
  - randomly initialize and train generation 0
  - train next generation
    - select, combine, and mutate
    - enqueue train requests containing:
      - generation ID
      - chromosome
    - collect train notifications
    - evaluate generation
      - evaluate Mixtures
      - attach MixtureEvals
    - attach GenEval
  
- request workers take a Chromosome as input
  - (an expert is the training output in addition to aux training input (e.g., initial weights)
    and metadata like final cost, cost over time during training, etc.)
  - train expert
    - preprocess data
    - train neural net
  - store Expert
  - broadcast training notification
  
  
- TODO
  - get running on cloud servers
  - distributed/parallel evaluation?
  - why is the GA overfitting the blind set? something about the mixtures must be degenerate
    - could have been the fact that I was accidentally using 30% testing set, 70% validation
  - things to try
    - feed training set into experts to build up state before evaluating validation set?
    - experts vote either -1 or 1, or their vote is weighed by their certainty (magnitude of output)
  - pick better initial weights: http://www.heatonresearch.com/encog/articles/nguyen-widrow-neural-network-weight.html
  - web UI
  - backtesting
  - catch NaN bug
