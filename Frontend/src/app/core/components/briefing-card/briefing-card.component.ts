import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-briefing-card',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './briefing-card.component.html',
  styleUrls: ['./briefing-card.component.scss']
})
export class BriefingCardComponent implements OnInit {
  briefing: string | null = null;
  loading = true;

  constructor(private http: HttpClient) {}

  ngOnInit(): void {
    this.http.get<{ briefing: string }>(`${environment.apiUrl}/dashboard/briefing`)
      .subscribe({
        next: (res) => {
          this.briefing = res.briefing;
          this.loading = false;
        },
        error: () => {
          this.loading = false;
        }
      });
  }
}