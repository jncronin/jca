/* Copyright (C) 2015-2016 by John Cronin
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:

 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.

 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

/* Pong for JCA */

#include <stdint.h>
#include <stddef.h>
#include "uart.h"

#define BALL 'O'
#define BAT 'I'

#define BALL_START_X	40
#define BALL_START_Y	12

#define BAT1_X			2
#define BAT2_X			77
#define BAT_START_Y		9

#define PLAY_LEFT 0
#define PLAY_RIGHT 80
#define PLAY_TOP 0
#define PLAY_BOTTOM 25

#define BALL_DX_START 50
#define BALL_DY_START 100

#define TIMER_CNT 10000

#define BAT_LENGTH 6

#define BAT_DY		150

#define SCORE_Y		(PLAY_BOTTOM - 1)
#define SCORE1_X	37
#define SCORE_HYPHEN	39
#define SCORE2_X	41

#define HITS_BETWEEN_SPEEDUP		1
#define SPEEDUP_AMOUNT	1
#define SPEEDUP_MAX	10

static int ball_x = 40;
static int ball_y = 12;
static int ball_dx = 1;
static int ball_dy = 1;

static uint32_t ball_x_cnt = 0;
static uint32_t ball_y_cnt = 0;
static uint32_t ball_x_max = BALL_DX_START;
static uint32_t ball_y_max = BALL_DY_START;

static uint32_t bat_y_cnt = 0;
static uint32_t bat_y_max = BAT_DY;

static int bat1_x = 2;
static int bat1_y = 14;
static int bat2_x = 77;
static int bat2_y = 14;

static int bat1_dy = 0;
static int bat2_dy = 0;

static int score1 = 0;
static int score2 = 0;

static int hits = 0;

static void putvga(int x, int y, char c)
{
	*(volatile uint8_t *)(0x1000000 + x + y * 128) = c;
}

static void update_ball(int newx, int newy)
{
	putvga(ball_x, ball_y, ' ');
	putvga(newx, newy, BALL);
	ball_x = newx;
	ball_y = newy;
}

static void update_score()
{
	char scr1 = '0' + score1;
	char scr2 = '0' + score2;
	if(score1 > 9)
		scr1 = 'x';
	if(score2 > 9)
		scr2 = 'x';
	
	putvga(SCORE1_X, SCORE_Y, scr1);
	putvga(SCORE_HYPHEN, SCORE_Y, '-');
	putvga(SCORE2_X, SCORE_Y, scr2);
}

static void draw_bat(int x, int y)
{
	for(int i = PLAY_TOP; i < PLAY_BOTTOM; i++)
		putvga(x, i, ' ');
	for(int i = 0; i < BAT_LENGTH; i++)
		putvga(x, y + i, BAT);
}

static void update_bat1(int newy)
{
	if(newy < bat1_y)
	{
		putvga(bat1_x, newy + BAT_LENGTH, ' ');
		putvga(bat1_x, newy, BAT);
	}
	else if(newy > bat1_y)
	{
		putvga(bat1_x, bat1_y, ' ');
		putvga(bat1_x, bat1_y + BAT_LENGTH, BAT);
	}
	bat1_y = newy;
}

static void update_bat2(int newy)
{
	if(newy < bat2_y)
	{
		putvga(bat2_x, newy + BAT_LENGTH, ' ');
		putvga(bat2_x, newy, BAT);
	}
	else if(newy > bat2_y)
	{
		putvga(bat2_x, bat2_y, ' ');
		putvga(bat2_x, bat2_y + BAT_LENGTH, BAT);
	}
	bat2_y = newy;
}

void irq();

static void restart()
{
	update_ball(BALL_START_X, BALL_START_Y);
	draw_bat(BAT1_X, BAT_START_Y);
	draw_bat(BAT2_X, BAT_START_Y);
	bat1_x = BAT1_X;
	bat2_x = BAT2_X;
	bat1_y = BAT_START_Y;
	bat2_y = BAT_START_Y;

	ball_x_max = BALL_DX_START;
	ball_y_max = BALL_DY_START;
	ball_x_cnt = 0;
	ball_y_cnt = 0;
	bat_y_cnt = 0;
	hits = 0;
}

void main()
{
	ball_dx = 1;
	ball_dy = 1;
	score1 = 0;
	score2 = 0;
	restart();

	// Set up timer interrupt
	*(volatile uint32_t *)0x1800004 = TIMER_CNT;
	*(volatile uint32_t *)0x4 = (uint32_t)irq;
	*(volatile uint32_t *)0x1c00004 = 0x1;

	while(1);
}

void tick()
{
	putchar('T');

	int new_ball_x = ball_x;
	int new_ball_y = ball_y;

	ball_x_cnt++;
	if(ball_x_cnt >= ball_x_max)
	{
		ball_x_cnt = 0;
		new_ball_x += ball_dx;
	}

	ball_y_cnt++;
	if(ball_y_cnt >= ball_y_max)
	{
		ball_y_cnt = 0;
		new_ball_y += ball_dy;
		if(new_ball_y >= PLAY_BOTTOM)
		{
			new_ball_y = PLAY_BOTTOM - 1;
			ball_dy = -1;
		}
		if(new_ball_y < PLAY_TOP)
		{
			new_ball_y = PLAY_TOP;
			ball_dy = 1;
		}
	}

	if(ball_x_cnt == 0 || ball_y_cnt == 0)
	{
		update_ball(new_ball_x, new_ball_y);

		if(ball_x == bat1_x + 1 &&
						ball_y >= bat1_y &&
						ball_y < bat1_y + BAT_LENGTH)
		{
			ball_dx = 1;
			hits++;
		}
		if(ball_x == bat2_x - 1 &&
						ball_y >= bat2_y &&
						ball_y < bat2_y + BAT_LENGTH)
		{
			ball_dx = -1;
			hits++;
		}

		if(hits >= HITS_BETWEEN_SPEEDUP)
		{
			hits = 0;
			ball_x_max -= SPEEDUP_AMOUNT;
			if(ball_x_max < SPEEDUP_MAX)
				ball_x_max = SPEEDUP_MAX;
			ball_y_max = ball_y_max - SPEEDUP_AMOUNT;
			if(ball_y_max < SPEEDUP_MAX)
				ball_y_max = SPEEDUP_MAX;
		}

		if(ball_x == PLAY_LEFT)
		{
			score2++;
			restart();
		}
		if(ball_x == PLAY_RIGHT - 1)
		{
			score1++;
			restart();
		}

		update_score();
	}

	// Always plan a desire to move the bat, even if we can't (used later
	//  for human movement)
	if((ball_y + ball_dy) > (bat1_y + BAT_LENGTH / 2 - 1))
		bat1_dy = 1;
	else if((ball_y + ball_dy) < (bat1_y + BAT_LENGTH / 2 - 1))
		bat1_dy = -1;
	else
		bat1_dy = 0;
	if((ball_y + ball_dy) > (bat2_y + BAT_LENGTH / 2 - 1))
		bat2_dy = 1;
	else if((ball_y + ball_dy) < (bat2_y + BAT_LENGTH / 2 - 1))
		bat2_dy = -1;
	else
		bat2_dy = 0;

	// Actually move bat if possible
	bat_y_cnt++;
	if(bat_y_cnt >= bat_y_max)
	{
		int new_bat1_y;
		int new_bat2_y;

		bat_y_cnt = 0;

		new_bat1_y = bat1_y + bat1_dy;
		new_bat2_y = bat2_y + bat2_dy;
		if(new_bat1_y < PLAY_TOP)
			new_bat1_y = PLAY_TOP;
		if(new_bat1_y + BAT_LENGTH >= PLAY_BOTTOM)
			new_bat1_y = PLAY_BOTTOM - BAT_LENGTH - 1;
		if(new_bat2_y < PLAY_TOP)
			bat2_y = PLAY_TOP;
		if(new_bat2_y + BAT_LENGTH >= PLAY_BOTTOM)
			bat2_y = PLAY_BOTTOM - BAT_LENGTH - 1;

		update_bat1(new_bat1_y);
		update_bat2(new_bat2_y);
	}

	// Reset timer
	*(volatile uint32_t *)0x1800000 = 0;

	// send EOI
	*(volatile uint32_t *)0x1c00008 = 0;
}
