(define-record bar (open low high close))

(define (gaussian x) (exp (* -1 x x 1/2)))

(define (dotprod u v)
  (if (null? u)
      0
      (+ (* (car u) (car v)) (dotprod (cdr u) (cdr v)))))

(define (take n ls)
  (if (= n 0)
      '()
      (cons (car ls) (take (- n 1) (cdr ls)))))

(define (skip n ls)
  (if (= n 0)
      ls
      (skip (- n 1) (cdr ls))))

(define (transform ds t)
  (reverse
   (let lp ([ds ds] [rev '()] [result '()] [pos 0])
     (if (null? ds)
	 result
	 (let ([new-rev (cons (car ds) rev)])
	   (let ([get-rev (lambda (n) (list-ref new-rev n))]
		 [get-result (lambda (n) (list-ref result (- n 1)))])
	     (lp (cdr ds) new-rev (cons (t get-rev get-result pos) result) (+ pos 1))))))))

(define (derivative ls)
  (transform ls
	     (lambda (s v pos)
	       (if (= pos 0)
		   0
		   (- (s 0) (s 1))))))

(define (sma period ls)
  (transform ls
	     (lambda (s v pos)
	       (if (= pos 0)
		   (s 0)
		   (let* ([window-size (min pos period)]
			  [last (* (v 1) window-size)])
		     (if (>= pos period)
			 (/ (+ last (s 0) (- (s period))) window-size)
			 (/ (+ last (s 0)) (+ window-size 1))))))))

(define (zlema period ls)
  (let* ([k (/ 2 (+ period 1))]
	 [one-minus-k (- 1 k)]
	 [lag (ceiling (/ (- period 1) 2))])
    (transform ls
	       (lambda (s v pos)
		 (if (>= pos lag)
		     (+ (* k (- (* 2 (s 0)) (s lag))) (* one-minus-k (v 1)))
		     (s 0))))))

(define num-inputs 9)
(define num-hidden 2)

(define (propagate inputs weights)
  (let ([node1-weights (take num-inputs weights)]
	[node2-weights (take num-inputs (skip num-inputs weights))]
	[output-weights (take num-hidden (skip (* num-hidden num-inputs) weights))])
  (dotprod
   (list (tanh (dotprod inputs node1-weights)) (gaussian (dotprod inputs node2-weights)))
   output-weights)))

