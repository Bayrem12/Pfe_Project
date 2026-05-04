import { Injectable, inject, OnDestroy } from '@angular/core';
import { BehaviorSubject, interval, Observable, switchMap, takeWhile, tap } from 'rxjs';
import { DashboardService } from './dashboard.service';
import { DashboardData } from '../models/dashboard.model';

@Injectable({
  providedIn: 'root'
})
export class RealTimeService implements OnDestroy {
  private dashboardService = inject(DashboardService);
  private isActiveSubject = new BehaviorSubject<boolean>(false);
  private refreshIntervalMs = 30000; // 30 seconds
  
  // Observable for real-time dashboard updates
  dashboardUpdates$ = this.isActiveSubject.pipe(
    switchMap(isActive => 
      isActive ? interval(this.refreshIntervalMs).pipe(
        switchMap(() => this.dashboardService.getDashboardData()),
        tap(data => console.log('Real-time update:', new Date().toLocaleTimeString()))
      ) : []
    )
  );

  get isActive(): boolean {
    return this.isActiveSubject.value;
  }

  startRealTimeUpdates(): void {
    console.log('Starting real-time dashboard updates...');
    this.isActiveSubject.next(true);
  }

  stopRealTimeUpdates(): void {
    console.log('Stopping real-time dashboard updates...');
    this.isActiveSubject.next(false);
  }

  toggleRealTimeUpdates(): void {
    const newState = !this.isActive;
    console.log(`Toggling real-time updates: ${newState ? 'ON' : 'OFF'}`);
    this.isActiveSubject.next(newState);
  }

  setRefreshInterval(intervalMs: number): void {
    this.refreshIntervalMs = intervalMs;
    if (this.isActive) {
      // Restart with new interval
      this.stopRealTimeUpdates();
      setTimeout(() => this.startRealTimeUpdates(), 100);
    }
  }

  // Manual refresh method
  refreshNow(): Observable<DashboardData> {
    console.log('Manual refresh triggered');
    return this.dashboardService.getDashboardData();
  }

  ngOnDestroy(): void {
    this.stopRealTimeUpdates();
  }
}
