use std::{thread, time::Instant};

pub fn p(arr: &[i32])-> i32{
    arr.iter().sum()
}

fn p_mt(arr:&'static [i32])->i32{
    arr.chunks(arr.len().div_ceil(12))
    .map(|chunk| thread::spawn(move || chunk.iter().sum::<i32>()))
    .collect::<Vec<_>>()
    .into_iter()
    .map(|h| h.join().unwrap())
    .sum()
}

fn main(){
    let arr: &'static [i32]=Box::leak(vec![2;200000000].into_boxed_slice());
    bench(arr, 50);
}

fn bench(arr:&'static [i32], iter:usize){
    let mut dur = Vec::with_capacity(iter);
    let mut res =0;
    for _ in 0..iter{
        let start = Instant::now();
        res = p_mt(arr);
        let elapsed = start.elapsed().as_secs_f32() * 1000.0;
        dur.push(elapsed);
    }
    dur.sort_by(|a,b| a.partial_cmp(b).unwrap());
    let mean: f32 = dur.iter().sum::<f32>() / dur.len() as f32;
    let median = dur[dur.len()/2];

    println!("{:.3}",mean);
    println!("{:.3}",median);
    println!("{}",res);

    let mut dur = Vec::with_capacity(iter);
    let mut res =0;
    for _ in 0..iter{
        let start = Instant::now();
        res = p(arr);
        let elapsed = start.elapsed().as_secs_f32() * 1000.0;
        dur.push(elapsed);
    }
    dur.sort_by(|a,b| a.partial_cmp(b).unwrap());
    let mean: f32 = dur.iter().sum::<f32>() / dur.len() as f32;
    let median = dur[dur.len()/2];

    println!("{:.3}",mean);
    println!("{:.3}",median);
    println!("{}",res);
}
