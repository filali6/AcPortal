import { Component, Input, Output, EventEmitter, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-modal',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './modal.component.html',
  styleUrl: './modal.component.scss'
})
export class ModalComponent implements OnDestroy {
  @Input() title = '';
  @Input() width = '480px';
  @Output() closed = new EventEmitter<void>();

  minimized = false;
  modalX = 0;
  modalY = 0;

  private isDragging = false;
  private dragStartX = 0;
  private dragStartY = 0;
  private modalStartX = 0;
  private modalStartY = 0;
  private onMove!: (e: MouseEvent) => void;
  private onUp!: () => void;

  onDragStart(event: MouseEvent): void {
    if (this.minimized) return;
    this.isDragging = true;
    this.dragStartX = event.clientX;
    this.dragStartY = event.clientY;
    this.modalStartX = this.modalX;
    this.modalStartY = this.modalY;

    this.onMove = (e: MouseEvent) => {
      if (!this.isDragging) return;
      this.modalX = this.modalStartX + (e.clientX - this.dragStartX);
      this.modalY = this.modalStartY + (e.clientY - this.dragStartY);
    };

    this.onUp = () => {
      this.isDragging = false;
      document.removeEventListener('mousemove', this.onMove);
      document.removeEventListener('mouseup', this.onUp);
    };

    document.addEventListener('mousemove', this.onMove);
    document.addEventListener('mouseup', this.onUp);
  }

  close(): void {
    this.closed.emit();
  }

  ngOnDestroy(): void {
    document.removeEventListener('mousemove', this.onMove);
    document.removeEventListener('mouseup', this.onUp);
  }
}